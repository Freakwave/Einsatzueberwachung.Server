using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Divera;
using Microsoft.AspNetCore.Components;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private IEinsatzService EinsatzService { get; set; } = default!;
    [Inject] private IArchivService ArchivService { get; set; } = default!;
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] private IDiveraService DiveraService { get; set; } = default!;
    [Inject] private IWeatherService WeatherService { get; set; } = default!;

    private Action<Team>? _teamAddedHandler;
    private Action<Team>? _teamRemovedHandler;
    private Action<Team>? _teamUpdatedHandler;
    private Action<GlobalNotesEntry>? _noteAddedHandler;
    private bool _serverHealthy;
    private bool _serverStatusLoading;
    private string _serverHealthText = "Unbekannt";
    private string _serverStatusError = string.Empty;
    private DateTime? _serverLastCheckedAt;

    private List<DiveraAlarm> _diveraAlarms = new();
    private System.Threading.Timer? _diveraTimer;

    private WeatherData? _weather;
    private FlugwetterData? _flugwetter;
    private bool _weatherLoading;
    private string _weatherError = string.Empty;

    protected override void OnInitialized()
    {
        _teamAddedHandler = _ => Refresh();
        _teamRemovedHandler = _ => Refresh();
        _teamUpdatedHandler = _ => Refresh();
        _noteAddedHandler = _ => Refresh();

        EinsatzService.EinsatzChanged += Refresh;
        EinsatzService.TeamAdded += _teamAddedHandler;
        EinsatzService.TeamRemoved += _teamRemovedHandler;
        EinsatzService.TeamUpdated += _teamUpdatedHandler;
        EinsatzService.NoteAdded += _noteAddedHandler;

        _ = RefreshServerStatusAsync();
    }

    protected override async Task OnInitializedAsync()
    {
        await PollDiveraAsync();

        var e = EinsatzService.CurrentEinsatz;
        if (!string.IsNullOrWhiteSpace(e.Einsatzort))
            await RefreshWeatherAsync();
    }

    private void ScheduleDiveraTimer()
    {
        _diveraTimer?.Dispose();
        var intervalMs = _diveraAlarms.Any()
            ? DiveraService.PollIntervalActiveSeconds * 1000
            : DiveraService.PollIntervalIdleSeconds * 1000;

        _diveraTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await PollDiveraAsync();
                StateHasChanged();
            });
        }, null, intervalMs, System.Threading.Timeout.Infinite);
    }

    private async Task PollDiveraAsync()
    {
        try
        {
            _diveraAlarms = await DiveraService.GetActiveAlarmsAsync();
        }
        catch
        {
            _diveraAlarms = new();
        }
        ScheduleDiveraTimer();
    }

    private void Refresh() => InvokeAsync(StateHasChanged);

    private async Task RefreshServerStatusAsync()
    {
        _serverStatusLoading = true;
        _serverStatusError = string.Empty;

        try
        {
            var client = HttpClientFactory.CreateClient();
            var response = await client.GetAsync("/health");

            _serverHealthy = response.IsSuccessStatusCode;
            _serverHealthText = _serverHealthy ? "OK" : $"Fehler ({(int)response.StatusCode})";
        }
        catch (Exception ex)
        {
            _serverHealthy = false;
            _serverHealthText = "Nicht erreichbar";
            _serverStatusError = ex.Message;
        }
        finally
        {
            _serverLastCheckedAt = DateTime.Now;
            _serverStatusLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshWeatherAsync()
    {
        var einsatzort = EinsatzService.CurrentEinsatz.Einsatzort;
        if (string.IsNullOrWhiteSpace(einsatzort)) return;

        _weatherLoading = true;
        _weatherError = string.Empty;
        StateHasChanged();

        try
        {
            _weather = await WeatherService.GetCurrentWeatherByAddressAsync(einsatzort);
            if (_weather == null)
            {
                _weatherError = "Keine Wetterdaten verfügbar.";
            }
            else
            {
                var elw = EinsatzService.CurrentEinsatz.ElwPosition;
                if (elw.HasValue)
                {
                    _flugwetter = await WeatherService.GetFlugwetterAsync(elw.Value.Latitude, elw.Value.Longitude);
                }
                else
                {
                    var coords = await WeatherService.GeocodeAddressAsync(einsatzort);
                    if (coords.HasValue)
                        _flugwetter = await WeatherService.GetFlugwetterAsync(coords.Value.Latitude, coords.Value.Longitude);
                }
            }
        }
        catch (Exception ex)
        {
            _weatherError = $"Wetter konnte nicht geladen werden: {ex.Message}";
        }
        finally
        {
            _weatherLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string DisplayDateTime(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd.MM.yyyy HH:mm") : "-";
    }

    public void Dispose()
    {
        EinsatzService.EinsatzChanged -= Refresh;
        if (_teamAddedHandler is not null) EinsatzService.TeamAdded -= _teamAddedHandler;
        if (_teamRemovedHandler is not null) EinsatzService.TeamRemoved -= _teamRemovedHandler;
        if (_teamUpdatedHandler is not null) EinsatzService.TeamUpdated -= _teamUpdatedHandler;
        if (_noteAddedHandler is not null) EinsatzService.NoteAdded -= _noteAddedHandler;
        _diveraTimer?.Dispose();
    }
}
