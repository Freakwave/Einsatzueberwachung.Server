using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class EinsatzLeitung : IDisposable
{
    [Inject] private IEinsatzService EinsatzService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string _activeTab = "uebersicht";
    private string _notizSubTab = "el";
    private string _noteFilter = "alle";

    private EinsatzData _e => EinsatzService.CurrentEinsatz;
    private int _teamsRunning => EinsatzService.Teams.Count(t => t.IsRunning);
    private int _teamsWarning => EinsatzService.Teams.Count(t => t.IsFirstWarning || t.IsSecondWarning);
    private int _teamsReady => EinsatzService.Teams.Count(t => !t.IsRunning && !t.IsPausing);

    private VermisstenInfo _vi = new();
    private string _saveMessage = string.Empty;
    private bool _saveIsError;

    private string _elNotizText = string.Empty;
    private string _elNotizPrefix = string.Empty;

    protected override void OnInitialized()
    {
        var existing = EinsatzService.CurrentEinsatz.VermisstenInfo;
        if (existing is not null)
            CloneViFrom(existing);

        EinsatzService.EinsatzChanged += OnStateChanged;
        EinsatzService.TeamAdded += OnTeamChanged;
        EinsatzService.TeamUpdated += OnTeamChanged;
        EinsatzService.TeamRemoved += OnTeamChanged;
        EinsatzService.NoteAdded += OnNoteChanged;
        EinsatzService.VermisstenInfoChanged += OnVermisstenChanged;
        EinsatzService.ElNotizAdded += OnStateChangedDirect;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try { await JS.InvokeVoidAsync("elDashboard.startClock", "el-live-clock"); }
        catch { /* Uhr-Fehler sollen die Seite nicht crashen */ }
    }

    private async Task SaveVermisstenAsync()
    {
        await EinsatzService.UpdateVermisstenInfoAsync(_vi);
        _saveMessage = "Gespeichert.";
        _saveIsError = false;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(2000);
        _saveMessage = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddElNotizAsync()
    {
        if (string.IsNullOrWhiteSpace(_elNotizText)) return;
        await EinsatzService.AddElNotizAsync(_elNotizText, _elNotizPrefix);
        _elNotizText = string.Empty;
    }

    private async Task DeleteElNotizAsync(string id)
    {
        await EinsatzService.DeleteElNotizAsync(id);
    }

    private void CloneViFrom(VermisstenInfo src)
    {
        _vi = new VermisstenInfo
        {
            Vorname = src.Vorname,
            Nachname = src.Nachname,
            Alter = src.Alter,
            Geburtsdatum = src.Geburtsdatum,
            Kleidung = src.Kleidung,
            Besonderheiten = src.Besonderheiten,
            ZuletztGesehenOrt = src.ZuletztGesehenOrt,
            ZuletztGesehenZeit = src.ZuletztGesehenZeit,
            ZuletztGesehenVon = src.ZuletztGesehenVon,
            Vorerkrankungen = src.Vorerkrankungen,
            Medikamente = src.Medikamente,
            Orientierung = src.Orientierung,
            Mobilitaet = src.Mobilitaet,
            Suizidrisiko = src.Suizidrisiko,
            Bewaffnet = src.Bewaffnet,
            PolizeiKontaktName = src.PolizeiKontaktName,
            PolizeiDienstnummer = src.PolizeiDienstnummer,
            PolizeiTelefon = src.PolizeiTelefon,
            PolizeiVermisstenmeldungAufgenommen = src.PolizeiVermisstenmeldungAufgenommen,
            PolizeiKoordinationBesprochen = src.PolizeiKoordinationBesprochen,
            PolizeiSuchabschnittAbgestimmt = src.PolizeiSuchabschnittAbgestimmt,
            PolizeiRueckmeldepflichtVereinbart = src.PolizeiRueckmeldepflichtVereinbart,
            PolizeiDatenschutzGeklaert = src.PolizeiDatenschutzGeklaert,
            BosEinheit = src.BosEinheit,
            BosZugfuehrer = src.BosZugfuehrer,
            BosFunkrufname = src.BosFunkrufname,
            BosAufgabenteilung = src.BosAufgabenteilung,
            BosAbschnittAbgestimmt = src.BosAbschnittAbgestimmt,
            BosRessourcenBesprochen = src.BosRessourcenBesprochen
        };
    }

    private void OnGeburtsdatumChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? string.Empty;
        _vi.Geburtsdatum = value;
        if (DateTime.TryParseExact(value, "dd.MM.yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var birth))
        {
            var today = DateTime.Today;
            var age = today.Year - birth.Year;
            if (birth.Date > today.AddYears(-age)) age--;
            _vi.Alter = age.ToString();
        }
    }

    private static string GetElTeamClass(Team t) => t switch
    {
        _ when t.IsSecondWarning => "el-team-critical",
        _ when t.IsFirstWarning  => "el-team-warning",
        _ when t.IsRunning       => "el-team-running",
        _ when t.IsPausing       => "el-team-pause",
        _                        => "el-team-ready"
    };

    private static string GetTimerClass(Team t) => t switch
    {
        _ when t.IsSecondWarning => "el-timer-critical",
        _ when t.IsFirstWarning  => "el-timer-warning",
        _ when t.IsRunning       => "el-timer-running",
        _                        => "el-timer-default"
    };

    private static string GetTeamHexColor(Team t) => t switch
    {
        _ when t.IsSecondWarning => "#dc3545",
        _ when t.IsFirstWarning  => "#ffc107",
        _ when t.IsRunning       => "#198754",
        _ when t.IsPausing       => "#546e7a",
        _                        => "#6c757d"
    };

    private static string OrientierungLabel(OrientierungsStatus s) => s switch
    {
        OrientierungsStatus.Gut => "Gut",
        OrientierungsStatus.Eingeschraenkt => "Eingeschränkt",
        _ => "Unbekannt"
    };

    private static string MobilitaetLabel(MobilitaetsStatus s) => s switch
    {
        MobilitaetsStatus.ZuFuss => "Zu Fuß",
        MobilitaetsStatus.Rollator => "Rollator",
        MobilitaetsStatus.Rollstuhl => "Rollstuhl",
        MobilitaetsStatus.Fahrzeug => "Fahrzeug",
        _ => "Unbekannt"
    };

    private void OnStateChanged()
        => InvokeAsync(StateHasChanged);

    private void OnTeamChanged(Team team)
        => InvokeAsync(StateHasChanged);

    private void OnNoteChanged(GlobalNotesEntry note)
        => InvokeAsync(StateHasChanged);

    private void OnStateChangedDirect()
        => InvokeAsync(StateHasChanged);

    private void OnVermisstenChanged()
    {
        if (_activeTab != "vermisst")
        {
            var latest = EinsatzService.CurrentEinsatz.VermisstenInfo;
            if (latest is not null)
                CloneViFrom(latest);
        }
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        EinsatzService.EinsatzChanged -= OnStateChanged;
        EinsatzService.TeamAdded -= OnTeamChanged;
        EinsatzService.TeamUpdated -= OnTeamChanged;
        EinsatzService.TeamRemoved -= OnTeamChanged;
        EinsatzService.NoteAdded -= OnNoteChanged;
        EinsatzService.VermisstenInfoChanged -= OnVermisstenChanged;
        EinsatzService.ElNotizAdded -= OnStateChangedDirect;
    }
}
