using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Server.Services;
using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IAsyncDisposable
{
    [Inject] private BrowserPreferencesService BrowserPrefs { get; set; } = default!;
    [Inject] private IEinsatzService EinsatzService { get; set; } = default!;
    [Inject] private TrainerNotificationService TrainerNotifications { get; set; } = default!;
    [Inject] private IWarningService WarningService { get; set; } = default!;
    [Inject] private ITimeService TimeService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _isDarkMode;
    private bool _sidebarCollapsed;
    private DotNetObjectReference<MainLayout>? _dotNetRef;
    private bool _showCriticalWarningPopup;
    private string _criticalWarningTitle = string.Empty;
    private string _criticalWarningMessage = string.Empty;
    private bool _audioEnableHintVisible;

    private bool _showExerciseEndedPopup;
    private string _exerciseEndedName = string.Empty;
    private string _exerciseEndedSummary = string.Empty;

    private bool HasActiveEinsatz =>
        !string.IsNullOrWhiteSpace(EinsatzService.CurrentEinsatz.Einsatzort)
        && EinsatzService.CurrentEinsatz.EinsatzEnde is null;

    private int RunningTeamsCount => EinsatzService.Teams.Count(team => team.IsRunning);

    private int PausingTeamsCount => EinsatzService.Teams.Count(team => team.IsPausing);

    private int ReadyTeamsCount => EinsatzService.Teams.Count(team => !team.IsDroneTeam && !team.IsSupportTeam && !team.IsRunning && !team.IsPausing);

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        EinsatzService.EinsatzChanged += OnEinsatzStateChanged;
        EinsatzService.TeamAdded += OnTeamStateChanged;
        EinsatzService.TeamUpdated += OnTeamStateChanged;
        EinsatzService.TeamRemoved += OnTeamStateChanged;
        EinsatzService.TeamWarningTriggered += OnTeamWarningTriggered;
        EinsatzService.DogPauseStarted += OnDogPauseStarted;
        TrainerNotifications.ExerciseEnded += OnExerciseEnded;
    }

    private void OnExerciseEnded(string exerciseName, string summary)
    {
        _ = InvokeAsync(() =>
        {
            _exerciseEndedName = exerciseName;
            _exerciseEndedSummary = summary;
            _showExerciseEndedPopup = true;
            StateHasChanged();
        });
    }

    private void DismissExerciseEndedPopup()
    {
        _showExerciseEndedPopup = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await BrowserPrefs.LoadAsync();
        _isDarkMode = BrowserPrefs.Preferences.IsDarkMode;
        _sidebarCollapsed = await JS.InvokeAsync<bool>("layoutTools.getSidebarCollapsed");
        _dotNetRef = DotNetObjectReference.Create(this);

        await JS.InvokeVoidAsync("themeSync.init", _dotNetRef, _isDarkMode);

        if (BrowserPrefs.Preferences.ThemeMode == "Auto")
        {
            await JS.InvokeVoidAsync("themeSync.watchSystemTheme");
        }
        else
        {
            await ApplyPersistedThemeAsync();
        }

        await JS.InvokeVoidAsync("initializeClock");
        StateHasChanged();
    }

    private void OnEinsatzStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnTeamStateChanged(Team team)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnTeamWarningTriggered(Team team, bool isSecondWarning)
    {
        _ = InvokeAsync(async () =>
        {
            var prefs = BrowserPrefs.Preferences;
            var audioStarted = true;
            if (prefs.SoundAlertsEnabled)
            {
                var soundType = isSecondWarning ? prefs.SecondWarningSound : prefs.FirstWarningSound;
                var frequency = isSecondWarning ? prefs.SecondWarningFrequency : prefs.FirstWarningFrequency;
                var shouldRepeat = isSecondWarning && prefs.RepeatSecondWarning;
                var repeatSeconds = Math.Max(1, prefs.RepeatWarningIntervalSeconds);

                try
                {
                    audioStarted = await JS.InvokeAsync<bool>(
                        "layoutTools.playWarningAlert",
                        soundType,
                        frequency,
                        prefs.SoundVolume,
                        shouldRepeat,
                        repeatSeconds);
                }
                catch
                {
                    audioStarted = false;
                }
            }

            _audioEnableHintVisible = prefs.SoundAlertsEnabled && !audioStarted;

            if (isSecondWarning)
            {
                _criticalWarningTitle = $"{team.TeamName} hat kritische Warnstufe erreicht";
                _criticalWarningMessage = $"Timer: {team.ElapsedTime:hh\\:mm\\:ss} · Bitte Teamstatus prüfen.";
                _showCriticalWarningPopup = true;
            }

            // Add a non-blocking warning toast for the first warning level
            if (!isSecondWarning)
            {
                WarningService.AddWarning(new WarningEntry
                {
                    Title = "Erste Warnstufe erreicht",
                    Message = $"{team.TeamName} – Timer: {team.ElapsedTime:hh\\:mm\\:ss}",
                    Level = WarningLevel.Warning,
                    TeamId = team.TeamId,
                    NavigationUrl = "/einsatz-monitor",
                    Source = "TeamTimer",
                    Timestamp = TimeService.Now
                });
            }

            StateHasChanged();
        });
    }

    private void OnDogPauseStarted(Team team)
    {
        WarningService.AddWarning(new WarningEntry
        {
            Title = "Hund braucht Pause",
            Message = $"{team.DogName} (Team: {team.TeamName}) – {team.RequiredPauseMinutes} Min. Pause erforderlich.",
            Level = WarningLevel.Warning,
            TeamId = team.TeamId,
            NavigationUrl = $"/einsatz-monitor#team-{team.TeamId}",
            Source = "DogPause",
            Timestamp = TimeService.Now
        });
    }

    private async Task DismissCriticalWarningAsync()
    {
        _showCriticalWarningPopup = false;
        _audioEnableHintVisible = false;

        try
        {
            await JS.InvokeVoidAsync("layoutTools.stopWarningAlert");
        }
        catch
        {
            // Ignore JS disconnects while closing popup.
        }
    }

    private async Task ApplyPersistedThemeAsync()
    {
        _isDarkMode = BrowserPrefs.Preferences.IsDarkMode;
        await JS.InvokeVoidAsync("themeSync.setTheme", _isDarkMode);
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            await ApplyPersistedThemeAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (JSDisconnectedException)
        {
            // Ignore navigation-time JS disconnects.
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation during shutdown/disconnect.
        }
    }

    private async Task ToggleSidebarAsync()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await JS.InvokeVoidAsync("layoutTools.setSidebarCollapsed", _sidebarCollapsed);
    }

    private void GoHome()
    {
        Navigation.NavigateTo("/");
    }

    private async Task ToggleThemeAsync()
    {
        if (BrowserPrefs.Preferences.ThemeMode != "Manual")
            return;

        _isDarkMode = !_isDarkMode;
        BrowserPrefs.Update(p => p.IsDarkMode = _isDarkMode);
        await BrowserPrefs.SaveAsync();
        await JS.InvokeVoidAsync("themeSync.setTheme", _isDarkMode);
    }

    private async Task ToggleFullscreenAsync()
    {
        await JS.InvokeVoidAsync("layoutTools.toggleFullscreen");
    }

    [JSInvokable]
    public async Task OnThemeChangedFromStorage(bool isDark)
    {
        _isDarkMode = isDark;
        BrowserPrefs.Update(p => p.IsDarkMode = isDark);
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        EinsatzService.EinsatzChanged -= OnEinsatzStateChanged;
        EinsatzService.TeamAdded -= OnTeamStateChanged;
        EinsatzService.TeamUpdated -= OnTeamStateChanged;
        EinsatzService.TeamRemoved -= OnTeamStateChanged;
        EinsatzService.TeamWarningTriggered -= OnTeamWarningTriggered;
        EinsatzService.DogPauseStarted -= OnDogPauseStarted;
        TrainerNotifications.ExerciseEnded -= OnExerciseEnded;

        try
        {
            await JS.InvokeVoidAsync("themeSync.stopWatchingSystemTheme");
            await JS.InvokeVoidAsync("themeSync.dispose");
            await JS.InvokeVoidAsync("layoutTools.stopWarningAlert");
        }
        catch
        {
            // Ignore disposal errors during disconnect/shutdown.
        }

        _dotNetRef?.Dispose();
    }
}
