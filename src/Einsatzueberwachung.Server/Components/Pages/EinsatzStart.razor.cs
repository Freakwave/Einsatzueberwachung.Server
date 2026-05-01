using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Divera;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class EinsatzStart
{
    [Inject] private IEinsatzService EinsatzService { get; set; } = default!;
    [Inject] private IMasterDataService MasterDataService { get; set; } = default!;
    [Inject] private ISettingsService SettingsService { get; set; } = default!;
    [Inject] private IDiveraService DiveraService { get; set; } = default!;
    [Inject] private ITrainingExerciseService TrainingExerciseService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private static readonly string[] AlarmTimeFormats =
    {
        "H:mm",
        "HH:mm",
        "H:mm:ss",
        "HH:mm:ss"
    };

    private EinsatzData _model = new()
    {
        EinsatzDatum = DateTime.Now,
        AnzahlTeams = 1
    };

    private List<PersonalEntry> _personal = new();
    private string _alarmStartTime = DateTime.Now.ToString("HH:mm");
    private DateTime? _clientNow;
    private string _status = string.Empty;
    private bool _error;
    private bool _busy;
    private bool _einsatzBereitsAktiv;
    private TrainingStartPreset? _trainerStartPreset;

    private List<DiveraAlarm> _diveraAlarms = new();
    private bool _diveraLoading;

    private sealed class ClientLocalNowDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }
    }

    private IEnumerable<PersonalEntry> ActivePersonnel => _personal
        .Where(person => person.IsActive)
        .OrderBy(person => person.Nachname)
        .ThenBy(person => person.Vorname);

    private IEnumerable<PersonalEntry> EinsatzleiterOptions => ActivePersonnel
        .Where(person => person.Skills.HasFlag(PersonalSkills.Einsatzleiter));

    private IEnumerable<PersonalEntry> FuehrungsassistentOptions => ActivePersonnel
        .Where(person => person.Skills.HasFlag(PersonalSkills.Fuehrungsassistent));

    protected override async Task OnInitializedAsync()
    {
        var einsatz = EinsatzService.CurrentEinsatz;
        _einsatzBereitsAktiv = !string.IsNullOrWhiteSpace(einsatz.Einsatzort)
            && einsatz.EinsatzEnde is null;

        if (_einsatzBereitsAktiv)
        {
            return;
        }

        var personalTask = MasterDataService.GetPersonalListAsync();
        var settingsTask = SettingsService.GetStaffelSettingsAsync();

        await Task.WhenAll(personalTask, settingsTask);

        _personal = personalTask.Result;
        var staffelSettings = settingsTask.Result;

        if (!string.IsNullOrWhiteSpace(staffelSettings.StaffelName))
        {
            _model.StaffelName = staffelSettings.StaffelName;
        }

        _model.StaffelAdresse = staffelSettings.StaffelAdresse;
        _model.StaffelTelefon = staffelSettings.StaffelTelefon;
        _model.StaffelEmail = staffelSettings.StaffelEmail;
        _model.StaffelLogoPfad = staffelSettings.StaffelLogoPfad;

        _trainerStartPreset = await TrainingExerciseService.GetStartPresetAsync(CancellationToken.None);
        if (_trainerStartPreset is not null)
        {
            ApplyTrainerPreset();
        }

        var alarmBase = _model.AlarmierungsZeit ?? DateTime.Now;
        _alarmStartTime = alarmBase.ToString("HH:mm");

        await LoadDiveraAlarmsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await RefreshClientNowAsync();

        if (_clientNow.HasValue)
        {
            var now = _clientNow.Value;
            _alarmStartTime = now.ToString("HH:mm");
            _model.AlarmierungsZeit = now;
            _model.EinsatzDatum = now;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshClientNowAsync()
    {
        try
        {
            var dto = await JS.InvokeAsync<ClientLocalNowDto>("layoutTools.getClientLocalNow");
            _clientNow = new DateTime(dto.Year, dto.Month, dto.Day, dto.Hour, dto.Minute, dto.Second, DateTimeKind.Unspecified);
        }
        catch
        {
            _clientNow = null;
        }
    }

    private void SetEinsatzleiter(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var p = _personal.FirstOrDefault(x => x.Id == id);
        if (p is not null) _model.Einsatzleiter = p.FullName;
    }

    private void SetFuehrungsassistent(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var p = _personal.FirstOrDefault(x => x.Id == id);
        if (p is not null) _model.Fuehrungsassistent = p.FullName;
    }

    private async Task StartAsync()
    {
        await RefreshClientNowAsync();

        if (string.IsNullOrWhiteSpace(_model.Einsatzort))
        {
            _status = "Bitte Einsatzort ausfüllen.";
            _error = true;
            return;
        }

        if (!TryPrepareAlarmTime())
        {
            _status = "Alarmzeit ungueltig. Bitte Format HH:mm verwenden.";
            _error = true;
            return;
        }

        _busy = true;
        _status = string.Empty;
        try
        {
            _model.EinsatzDatum = _clientNow ?? DateTime.Now;
            await EinsatzService.StartEinsatzAsync(_model);
            if (_trainerStartPreset is not null)
            {
                await TrainingExerciseService.ClearStartPresetAsync(CancellationToken.None);
            }
            _status = "Einsatz wurde gestartet.";
            _error = false;
            await Task.Delay(800);
            Navigation.NavigateTo("/einsatz-monitor");
        }
        catch (Exception ex)
        {
            _status = $"Fehler: {ex.Message}";
            _error = true;
        }
        finally
        {
            _busy = false;
        }
    }

    private bool TryPrepareAlarmTime()
    {
        var now = _clientNow ?? DateTime.Now;

        if (string.IsNullOrWhiteSpace(_alarmStartTime))
        {
            _model.AlarmierungsZeit = now;
            return true;
        }

        if (DateTime.TryParseExact(_alarmStartTime.Trim(), AlarmTimeFormats, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), System.Globalization.DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(_alarmStartTime.Trim(), System.Globalization.CultureInfo.GetCultureInfo("de-DE"), System.Globalization.DateTimeStyles.None, out parsed))
        {
            parsed = new DateTime(now.Year, now.Month, now.Day, parsed.Hour, parsed.Minute, 0);

            if (parsed > now.AddMinutes(1))
            {
                parsed = parsed.AddDays(-1);
            }

            _model.AlarmierungsZeit = parsed;
            return true;
        }

        return false;
    }

    private async Task LoadDiveraAlarmsAsync()
    {
        _diveraLoading = true;
        try
        {
            _diveraAlarms = await DiveraService.GetActiveAlarmsAsync();
        }
        catch
        {
            _diveraAlarms = new();
        }
        finally
        {
            _diveraLoading = false;
        }
    }

    private void ImportFromDivera(DiveraAlarm alarm)
    {
        _model.Einsatzort = alarm.Address;

        if (alarm.Lat.HasValue && alarm.Lng.HasValue)
        {
            _model.MapAddress = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:F6},{1:F6}", alarm.Lat.Value, alarm.Lng.Value);
            _model.ElwPosition = (alarm.Lat.Value, alarm.Lng.Value);
        }
        else
        {
            _model.MapAddress = alarm.Address;
        }

        _model.Stichwort = alarm.Title;
        _model.Alarmiert = alarm.Caller;

        _model.EinsatzNummer = !string.IsNullOrWhiteSpace(alarm.ForeignId)
            ? alarm.ForeignId
            : $"D-{alarm.Id}";

        _model.IstEinsatz = true;

        var lagetext = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(alarm.Text))
            lagetext.AppendLine(alarm.Text);
        if (!string.IsNullOrWhiteSpace(alarm.Remark))
        {
            if (lagetext.Length > 0) lagetext.AppendLine();
            lagetext.Append("Hinweis: ").Append(alarm.Remark);
        }
        _model.ExportPfad = lagetext.Length > 0 ? lagetext.ToString().Trim() : alarm.Title;

        if (alarm.Date != default)
        {
            _model.AlarmierungsZeit = alarm.Date;
            _alarmStartTime = alarm.Date.ToString("HH:mm");
        }

        StateHasChanged();
    }

    private void ApplyTrainerPreset()
    {
        if (_trainerStartPreset is null)
        {
            return;
        }

        _model.IstEinsatz = false;
        _model.Einsatzort = _trainerStartPreset.SuggestedLocation;
        _model.Stichwort = _trainerStartPreset.ScenarioCategory;
        _model.Alarmiert = "Trainer-Modul";
        _model.ExportPfad = _trainerStartPreset.BriefingText;

        if (string.IsNullOrWhiteSpace(_model.EinsatzNummer))
        {
            _model.EinsatzNummer = $"U-{DateTime.Now:yyyyMMddHHmm}";
        }
    }

    private async Task ClearTrainerPresetAsync()
    {
        await TrainingExerciseService.ClearStartPresetAsync(CancellationToken.None);
        _trainerStartPreset = null;
    }
}
