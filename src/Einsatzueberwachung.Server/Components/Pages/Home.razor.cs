using System.Reflection;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Divera;
using Microsoft.AspNetCore.Components;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class Home : IAsyncDisposable
{
    [Inject] private IHomeNotesService HomeNotesService { get; set; } = default!;
    [Inject] private IArchivService ArchivService { get; set; } = default!;
    [Inject] private IDiveraService DiveraService { get; set; } = default!;
    [Inject] private ISettingsService SettingsService { get; set; } = default!;
    [Inject] private IEinsatzService EinsatzService { get; set; } = default!;

    private List<HomeNoteEntry> _notes = [];
    private string _newNoteText = string.Empty;

    private ArchivStatistics? _stats;
    private List<ArchivedEinsatz> _recentEinsaetze = [];

    private List<DiveraAlarm> _diveraAlarms = [];
    private System.Threading.Timer? _diveraTimer;
    private System.Threading.Timer? _clockTimer;

    private DateTime _now = DateTime.Now;
    private string _staffelName = string.Empty;
    private bool _logoVisible = true;

    private bool _einsatzAktiv;
    private string _einsatzOrt = string.Empty;

    private string _appVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    private string AvgPersonal =>
        _stats is { GesamtAnzahl: > 0 }
            ? (_stats.GesamtPersonalEinsaetze / (double)_stats.GesamtAnzahl).ToString("0.0")
            : "—";

    private string AvgDuration =>
        _stats is { GesamtAnzahl: > 0 } && _stats.DurchschnittlicheDauer > TimeSpan.Zero
            ? _stats.DurchschnittlicheDauer.ToString(@"h\:mm")
            : "—";

    protected override async Task OnInitializedAsync()
    {
        _notes = await HomeNotesService.GetNotesAsync();

        var staffelSettings = await SettingsService.GetStaffelSettingsAsync();
        _staffelName = staffelSettings.StaffelName ?? string.Empty;

        _stats = await ArchivService.GetStatisticsAsync();
        var all = await ArchivService.GetAllArchivedAsync();
        _recentEinsaetze = all.OrderByDescending(e => e.ArchivedAt).Take(3).ToList();

        RefreshEinsatzState();
        EinsatzService.EinsatzChanged += OnEinsatzChanged;

        await PollDiveraAsync();

        _clockTimer = new System.Threading.Timer(async _ =>
        {
            _now = DateTime.Now;
            await InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void RefreshEinsatzState()
    {
        var e = EinsatzService.CurrentEinsatz;
        _einsatzAktiv = !string.IsNullOrWhiteSpace(e.Einsatzort);
        _einsatzOrt = e.Einsatzort ?? string.Empty;
    }

    private void OnEinsatzChanged()
    {
        RefreshEinsatzState();
        InvokeAsync(StateHasChanged);
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
        try { _diveraAlarms = await DiveraService.GetActiveAlarmsAsync(); }
        catch { _diveraAlarms = []; }
        ScheduleDiveraTimer();
    }

    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(_newNoteText)) return;
        await HomeNotesService.AddNoteAsync(_newNoteText);
        _newNoteText = string.Empty;
        _notes = await HomeNotesService.GetNotesAsync();
    }

    private async Task DeleteNoteAsync(string id)
    {
        await HomeNotesService.DeleteNoteAsync(id);
        _notes = await HomeNotesService.GetNotesAsync();
    }

    private async Task OnNoteKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await AddNoteAsync();
    }

    private void OnLogoError() => _logoVisible = false;

    public async ValueTask DisposeAsync()
    {
        EinsatzService.EinsatzChanged -= OnEinsatzChanged;
        if (_clockTimer is not null) await _clockTimer.DisposeAsync();
        if (_diveraTimer is not null) await _diveraTimer.DisposeAsync();
    }
}
