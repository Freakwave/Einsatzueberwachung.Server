using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Einsatzueberwachung.Server.Components.Pages;

public partial class Trainer
{
    [Inject] ITrainingExerciseService TrainingExerciseService { get; set; } = default!;
    [Inject] ITrainingScenarioSuggestionService TrainingScenarioSuggestionService { get; set; } = default!;
    [Inject] IEinsatzService EinsatzService { get; set; } = default!;
    [Inject] IArchivService ArchivService { get; set; } = default!;
    [Inject] Einsatzueberwachung.Server.Training.TrainerNotificationService TrainerNotifications { get; set; } = default!;
    [Inject] IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] NavigationManager Navigation { get; set; } = default!;

    private static readonly List<ScenarioTemplate> ScenarioTemplates =
    [
        new(
            "lage-wald",
            "Vermisstensuche im Wald",
            "Sucheinsatz",
            "Eine 76-jaehrige Person wird seit 2 Stunden vermisst. Letzter Sichtkontakt am Waldparkplatz, Temperatur faellt, Licht wird schlechter.",
            [
                "Wie teilen die Teams die Suchsektoren auf?",
                "Wann wird auf Flaechensuche statt Spur umgestellt?",
                "Welche Sicherheitsregeln gelten bei Dunkelheit?"
            ],
            ["Sim-Team Alpha", "Sim-Team Bravo", "Sim-Team Drohne"]),
        new(
            "lage-mantrailing",
            "Mantrailing Innenstadt",
            "Mantrailing",
            "Startpunkt ist ein Busbahnhof. Die Spur fuehrt durch stark frequentierte Bereiche mit vielen Ablenkungen und Wetterwechsel.",
            [
                "Welche Taktik wird bei Spurverlust angewendet?",
                "Wie wird die Kommunikation mit der Einsatzleitung priorisiert?",
                "Wann wird eine Rueckfallebene aktiviert?"
            ],
            ["Sim-Team Trail 1", "Sim-Team Trail 2", "Sim-Lageleitung"]),
        new(
            "lage-flaeche-drohne",
            "Flaechensuche mit Drohnenunterstuetzung",
            "Vermisstensuche",
            "Eine unklare Lage mit grossem Suchgebiet und eingeschraenkter Sicht. Hundeteams und Drohne muessen eng koordiniert werden.",
            [
                "Welche Prioritaeten gelten bei der Sektorplanung?",
                "Wie werden Luft- und Bodenerkenntnisse zusammengefuehrt?",
                "Wie wird auf einen medizinischen Fund reagiert?"
            ],
            ["Sim-Team Boden", "Sim-Team Hund", "Sim-Team Drohne"])
    ];

    // ── Tab-Navigation ────────────────────────────────────────────────────────
    private string _activeTab = "start";

    private List<TrainingExerciseRecord> _exercises = new();
    private TrainingExerciseRecord? _selectedExercise;
    private string _selectedExerciseId = string.Empty;
    private string _selectedScenarioId = ScenarioTemplates[0].Id;

    private string SelectedExerciseId
    {
        get => _selectedExerciseId;
        set
        {
            _selectedExerciseId = value;
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            RefreshSuggestions();
        }
    }

    private string SelectedScenarioId
    {
        get => _selectedScenarioId;
        set
        {
            _selectedScenarioId = value;
            RefreshSuggestions();
        }
    }

    private ScenarioTemplate SelectedScenario =>
        ScenarioTemplates.FirstOrDefault(x => x.Id == _selectedScenarioId) ?? ScenarioTemplates[0];

    private string _newExerciseName = string.Empty;

    private string _entryType = "lage";
    private string _entryText = string.Empty;
    private string _entryParticipantId = string.Empty;
    private string _entryParticipantName = string.Empty;

    private string _decisionParticipantName = string.Empty;
    private string _decisionCategory = string.Empty;
    private string _decisionText = string.Empty;
    private string _decisionRationale = string.Empty;

    private bool _busy;
    private string _statusMessage = string.Empty;
    private bool _statusError;
    private bool _authChecked;
    private bool _trainerAuthenticated;
    private string _trainerPassword = string.Empty;
    private bool _trainerLoginBusy;
    private string _trainerAccessStatus = string.Empty;
    private bool _trainerAccessError;
    private List<Team> LiveTeams => EinsatzService.Teams.OrderBy(t => t.TeamName).ToList();
    private string _commFilter = "alle";
    private List<GlobalNotesEntry> _liveComms = new();
    private readonly List<QuickTrainerReaction> _teamJoinReactions = new();

    private string _suggestionHint = string.Empty;
    private TrainingScenarioSuggestionResult? _suggestions;

    private bool _showEndExerciseConfirm;
    private string _endSummary = string.Empty;

    private Timer? _refreshTimer;
    private Timer? _uiTimer;

    // Neue Übungserstellung
    private int? _newExerciseDurationMinutes;

    // Zeitgesteuerte Events
    private int _schedDelay = 15;
    private string _schedType = "funk";
    private string _schedText = string.Empty;

    // Stressoren-Katalog (statisch, speziell fuer EL/FA-Schulung)
    private static readonly IReadOnlyList<StressorCategory> StressorCatalog =
    [
        new("Kommunikation", [
            "Leitstelle fordert Lagemeldung in 5 Minuten",
            "Funkkommunikation auf Kanal 1 gestoert — auf Kanal 2 wechseln",
            "Medien erscheinen am Einsatzort — Presseanfrage eingegangen",
            "Einsatzleiter von Leitstelle direkt angefordert"
        ]),
        new("Ressourcen", [
            "Zweites Suchhundeteam trifft ein — Einweisung und Sektorzuweisung erforderlich",
            "Drohnenakku leer — Drohne muss landen, Neuplanung der Luftabdeckung",
            "Suche ueberschreitet geplante Dauer — Kraefte melden Erschoepfung",
            "Fuehrungsassistent: Kraeftenachweis aktualisieren und an EL melden"
        ]),
        new("Lageaenderung", [
            "Zeuge meldet Sichtung in Abschnitt C — abweichend vom aktuellen Suchgebiet",
            "Person gefunden, aber bewusstlos — Rettungsdienst sofort anfordern",
            "Hund kehrt ohne Fund zurueck — neue Sektorzuweisung erforderlich",
            "Vermisstensuche ausgeweitet: Neue Koordinaten erhalten"
        ]),
        new("Koordination EL/FA", [
            "Feuerwehr fordert Freigabe des Abschnitts fuer eigene Massnahmen",
            "Polizei uebernimmt Einsatzleitung — Uebergabe erforderlich",
            "Paralleleinsatz in Nachbargemeinde — Kraefteabzug wird angefragt",
            "Fuehrungsassistent: Einsatzdokumentation sofort sichern und EL vorlegen"
        ])
    ];

    protected override async Task OnInitializedAsync()
    {
        EinsatzService.TeamAdded += OnTeamAdded;
        EinsatzService.TeamUpdated += OnTeamUpdated;
        EinsatzService.TeamRemoved += OnTeamRemoved;
        EinsatzService.EinsatzChanged += OnEinsatzChanged;
        EinsatzService.NoteAdded += OnNoteAdded;

        await RefreshTrainerAuthStatusAsync();
        if (!_trainerAuthenticated)
        {
            return;
        }

        await ReloadExercisesAsync();
        RefreshSuggestions();
        RefreshLiveComms();

        // Automatische Aktualisierung alle 10 Sekunden
        _refreshTimer = new Timer(async _ =>
        {
            await ReloadExercisesAsync();
            await CheckAndFireScheduledEventsAsync();
            await InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        // UI-Uhr: 1-Sekunden-Tick nur fuer Countdown-Anzeige
        _uiTimer = new Timer(_ => InvokeAsync(StateHasChanged), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void SetCommFilter(string filter)
    {
        _commFilter = filter;
        RefreshLiveComms();
    }

    private void RefreshLiveComms()
    {
        IEnumerable<GlobalNotesEntry> query = EinsatzService.GlobalNotes
            .OrderByDescending(n => n.Timestamp);

        if (_commFilter == "funk")
        {
            query = query.Where(IsRadio);
        }
        else if (_commFilter == "notiz")
        {
            query = query.Where(n => !IsRadio(n));
        }

        _liveComms = query.Take(80).ToList();
    }

    private static bool IsRadio(GlobalNotesEntry note) =>
        string.Equals(note.SourceType, "Funk", StringComparison.OrdinalIgnoreCase);

    private static string GetSourceLabel(GlobalNotesEntry note)
    {
        if (!string.IsNullOrWhiteSpace(note.SourceTeamName))
        {
            return note.SourceTeamName;
        }

        if (!string.IsNullOrWhiteSpace(note.CreatedBy))
        {
            return note.CreatedBy;
        }

        return "Unbekannt";
    }

    private void PrepareReplyForNote(GlobalNotesEntry note)
    {
        _entryType = IsRadio(note) ? "funk" : "feedback";
        _entryParticipantName = note.SourceTeamName;
        _entryText = string.IsNullOrWhiteSpace(note.SourceTeamName)
            ? "Rueckmeldung erhalten. Bitte naechsten Schritt melden."
            : $"{note.SourceTeamName}: Rueckmeldung erhalten. Bitte Status und naechsten Schritt melden.";
    }

    private async Task SendQuickReactionAsync(QuickTrainerReaction reaction)
    {
        _entryType = "funk";
        _entryParticipantName = reaction.TeamName;
        _entryText = reaction.Text;

        await AddTrainerEntryAsync();
        _teamJoinReactions.RemoveAll(r => r.TeamName == reaction.TeamName);
    }

    private void AddTeamJoinReaction(Team team)
    {
        if (string.IsNullOrWhiteSpace(team.TeamName))
        {
            return;
        }

        if (_teamJoinReactions.Any(r => string.Equals(r.TeamName, team.TeamName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _teamJoinReactions.Insert(0, new QuickTrainerReaction(
            team.TeamName,
            $"{team.TeamName}: Teamstart bestaetigt. Startet Suche im zugewiesenen Bereich und meldet ersten Status in 5 Minuten."));

        if (_teamJoinReactions.Count > 8)
        {
            _teamJoinReactions.RemoveRange(8, _teamJoinReactions.Count - 8);
        }
    }

    private Task PrepareStartMaskAsync()
    {
        var preset = new TrainingStartPreset
        {
            ExerciseId = _selectedExerciseId,
            ExerciseName = string.IsNullOrWhiteSpace(_newExerciseName)
                ? SelectedScenario.Title
                : _newExerciseName.Trim(),
            ScenarioCategory = SelectedScenario.Category,
            SuggestedLocation = SelectedScenario.Title,
            BriefingText = BuildStartBriefing(SelectedScenario),
            PreparedAtUtc = DateTime.UtcNow
        };

        return SavePresetAndSetStatusAsync(preset);
    }

    private async Task SavePresetAndSetStatusAsync(TrainingStartPreset preset)
    {
        try
        {
            await TrainingExerciseService.SetStartPresetAsync(preset, CancellationToken.None);
            SetStatus("Maske 'Neuer Einsatz' wurde fuer Teilnehmer vorbefuellt.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Vorbefuellen fehlgeschlagen: {ex.Message}", true);
        }
    }

    private async Task LoginTrainerAsync()
    {
        _trainerAccessStatus = string.Empty;
        _trainerAccessError = false;

        if (string.IsNullOrWhiteSpace(_trainerPassword))
        {
            _trainerAccessStatus = "Bitte Trainer-Passwort eingeben.";
            _trainerAccessError = true;
            return;
        }

        _trainerLoginBusy = true;
        try
        {
            var client = HttpClientFactory.CreateClient();
            client.BaseAddress = new Uri(Navigation.BaseUri);

            var response = await client.PostAsJsonAsync("api/trainer-auth/login", new { password = _trainerPassword });
            if (!response.IsSuccessStatusCode)
            {
                _trainerAccessStatus = "Anmeldung fehlgeschlagen. Passwort ungueltig.";
                _trainerAccessError = true;
                return;
            }

            _trainerAuthenticated = true;
            _trainerPassword = string.Empty;
            _trainerAccessStatus = "Anmeldung erfolgreich.";
            _trainerAccessError = false;

            await ReloadExercisesAsync();
            RefreshSuggestions();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            _trainerAccessStatus = $"Trainer-Anmeldung nicht moeglich: {ex.Message}";
            _trainerAccessError = true;
        }
        finally
        {
            _trainerLoginBusy = false;
        }
    }

    private async Task RefreshTrainerAuthStatusAsync()
    {
        try
        {
            var client = HttpClientFactory.CreateClient();
            client.BaseAddress = new Uri(Navigation.BaseUri);
            var status = await client.GetFromJsonAsync<TrainerAuthStatusDto>("api/trainer-auth/status");
            _trainerAuthenticated = status?.Authenticated == true;
        }
        catch
        {
            _trainerAuthenticated = false;
        }
        finally
        {
            _authChecked = true;
        }
    }

    private async Task HandleLoginKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await LoginTrainerAsync();
        }
    }

    private async Task ReloadExercisesAsync()
    {
        var list = await TrainingExerciseService.GetExercisesAsync(CancellationToken.None);
        _exercises = list.ToList();

        if (!string.IsNullOrWhiteSpace(_selectedExerciseId))
        {
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
        }
    }

    private async Task CreateExerciseAsync()
    {
        _busy = true;
        try
        {
            var exerciseName = string.IsNullOrWhiteSpace(_newExerciseName)
                ? $"{SelectedScenario.Title} ({DateTime.Now:dd.MM HH:mm})"
                : _newExerciseName.Trim();

            var created = await TrainingExerciseService.CreateExerciseAsync(
                new CreateTrainingExerciseRequest(
                    ExternalReference: SelectedScenario.Id,
                    Name: exerciseName,
                    Scenario: $"{SelectedScenario.Category}: {SelectedScenario.Briefing}",
                    Location: "Simulation",
                    PlannedStartUtc: DateTime.UtcNow,
                    IsTraining: true,
                    Initiator: "TrainerPortal",
                    PlannedDurationMinutes: _newExerciseDurationMinutes > 0 ? _newExerciseDurationMinutes : null),
                CancellationToken.None);

            _selectedExerciseId = created.ExerciseId;
            _newExerciseName = string.Empty;

            await TrainingExerciseService.AddTrainerEntryAsync(
                created.ExerciseId,
                new AddTrainingTrainerEntryRequest(
                    EntryType: "lage",
                    Text: $"Startlage: {SelectedScenario.Briefing}",
                    ParticipantId: string.Empty,
                    ParticipantName: "Lageleitung",
                    OccurredAtUtc: DateTime.UtcNow,
                    IsTraining: true,
                    SourceSystem: "TrainerPortal",
                    SourceUser: "Trainer"),
                CancellationToken.None);

            await TrainingExerciseService.SetStartPresetAsync(
                new TrainingStartPreset
                {
                    ExerciseId = created.ExerciseId,
                    ExerciseName = created.Name,
                    ScenarioCategory = SelectedScenario.Category,
                    SuggestedLocation = SelectedScenario.Title,
                    BriefingText = BuildStartBriefing(SelectedScenario),
                    PreparedAtUtc = DateTime.UtcNow
                },
                CancellationToken.None);

            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus("Uebung wurde gestartet und die Maske 'Neuer Einsatz' automatisch vorbereitet.", false);
            _activeTab = "aktuell";
        }
        catch (Exception ex)
        {
            SetStatus($"Uebung konnte nicht gestartet werden: {ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task AddParticipantDecisionAsync()
    {
        if (_selectedExercise is null)
        {
            SetStatus("Bitte zuerst eine Uebung auswaehlen.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_decisionText))
        {
            SetStatus("Bitte eine Entscheidung eingeben.", true);
            return;
        }

        _busy = true;
        try
        {
            await TrainingExerciseService.MirrorDecisionAsync(
                _selectedExercise.Id,
                new MirrorTrainingDecisionRequest(
                    Category: string.IsNullOrWhiteSpace(_decisionCategory) ? "Teilnehmer-Entscheidung" : _decisionCategory,
                    Decision: _decisionText,
                    Rationale: _decisionRationale,
                    OccurredAtUtc: DateTime.UtcNow,
                    IsTraining: true,
                    SourceSystem: "TrainerPortal",
                    SourceUser: string.IsNullOrWhiteSpace(_decisionParticipantName) ? "Teilnehmer" : _decisionParticipantName),
                CancellationToken.None);

            _decisionText = string.Empty;
            _decisionRationale = string.Empty;
            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus("Teilnehmer-Entscheidung wurde gespeichert.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Entscheidung konnte nicht gespeichert werden: {ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task AddTrainerEntryAsync()
    {
        if (_selectedExercise is null)
        {
            SetStatus("Bitte zuerst eine Uebung auswaehlen.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_entryText))
        {
            SetStatus("Bitte zuerst einen Eintragstext erfassen.", true);
            return;
        }

        _busy = true;
        try
        {
            await TrainingExerciseService.AddTrainerEntryAsync(
                _selectedExercise.Id,
                new AddTrainingTrainerEntryRequest(
                    EntryType: _entryType,
                    Text: _entryText,
                    ParticipantId: _entryParticipantId,
                    ParticipantName: _entryParticipantName,
                    OccurredAtUtc: DateTime.UtcNow,
                    IsTraining: true,
                    SourceSystem: "TrainerPortal",
                    SourceUser: "Trainer"),
                CancellationToken.None);

            // Immer ins live Funk/Notizen-System pushen — unabhaengig ob ein Team gewaehlt ist.
            // Lagemeldungen und Funksprueche erscheinen als "Funk", Rueckmeldungen als "Notiz".
            var (noteSourceId, noteSourceName) = ResolveTrainerNoteSource();
            var noteSourceType = _entryType is "funk" or "lage" ? "Funk" : "Notiz";
            var noteText = _entryType switch
            {
                "lage"     => $"[Lage] {_entryText}",
                "feedback" => $"[Trainer] {_entryText}",
                _          => _entryText
            };
            var noteEntryType = _entryType is "funk" or "lage"
                ? GlobalNotesEntryType.EinsatzUpdate
                : GlobalNotesEntryType.Manual;

            await EinsatzService.AddGlobalNoteWithSourceAsync(
                noteText,
                noteSourceId,
                noteSourceName,
                noteSourceType,
                noteEntryType,
                "Trainer");

            _entryText = string.Empty;
            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus("Trainer-Update wurde gespeichert.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Update konnte nicht gespeichert werden: {ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// Gibt die Note-Quelle zurueck: aktives Team falls gewaehlt, sonst Trainer.
    /// </summary>
    private (string Id, string Name) ResolveTrainerNoteSource()
    {
        if (!string.IsNullOrWhiteSpace(_entryParticipantName))
        {
            var liveTeam = EinsatzService.Teams.FirstOrDefault(t =>
                string.Equals(t.TeamName, _entryParticipantName, StringComparison.OrdinalIgnoreCase));
            if (liveTeam is not null)
                return (liveTeam.TeamId, $"Trainer \u2192 {liveTeam.TeamName}");
        }
        return ("trainer", "Trainer");
    }

    private void RefreshSuggestions()
    {
        _suggestions = TrainingScenarioSuggestionService.BuildSuggestions(
            isExerciseMode: _selectedExercise is not null,
            location: SelectedScenario.Title,
            teams: Array.Empty<Team>(),
            hint: _suggestionHint);
    }

    // ── Timer-Hilfsmethoden ──────────────────────────────────────────────────

    private string GetElapsedDisplay()
    {
        if (_selectedExercise is null) return "--:--:--";
        var elapsed = DateTime.UtcNow - _selectedExercise.CreatedAtUtc;
        return elapsed.ToString(@"hh\:mm\:ss");
    }

    private TimeSpan? GetRemainingTime()
    {
        if (_selectedExercise?.PlannedDurationMinutes is null) return null;
        var end = _selectedExercise.CreatedAtUtc + TimeSpan.FromMinutes(_selectedExercise.PlannedDurationMinutes.Value);
        return end - DateTime.UtcNow;
    }

    private static string GetEscalationBadgeClass(int level) => level switch
    {
        1 => "text-bg-warning",
        2 => "text-bg-danger",
        3 => "text-bg-dark",
        _ => "text-bg-secondary"
    };

    // ── Zeitgesteuerte Events ─────────────────────────────────────────────────

    private async Task AddScheduledEventAsync()
    {
        if (_selectedExercise is null || string.IsNullOrWhiteSpace(_schedText)) return;
        _busy = true;
        try
        {
            await TrainingExerciseService.AddScheduledEventAsync(
                _selectedExercise.Id,
                new AddScheduledEventRequest(_schedDelay, _schedText.Trim(), _schedType, string.Empty),
                CancellationToken.None);
            _schedText = string.Empty;
            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus("Zeitgesteuertes Event wurde geplant.", false);
        }
        catch (Exception ex) { SetStatus($"Fehler: {ex.Message}", true); }
        finally { _busy = false; }
    }

    private async Task RemoveScheduledEventAsync(string eventId)
    {
        if (_selectedExercise is null) return;
        _busy = true;
        try
        {
            await TrainingExerciseService.RemoveScheduledEventAsync(_selectedExercise.Id, eventId, CancellationToken.None);
            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
        }
        catch (Exception ex) { SetStatus($"Fehler: {ex.Message}", true); }
        finally { _busy = false; }
    }

    private async Task CheckAndFireScheduledEventsAsync()
    {
        if (_selectedExercise is null || _selectedExercise.Status != "open") return;
        var pending = _selectedExercise.ScheduledEvents
            .Where(e => !e.IsFired && DateTime.UtcNow >= _selectedExercise.CreatedAtUtc + TimeSpan.FromMinutes(e.DelayMinutes))
            .ToList();

        foreach (var ev in pending)
        {
            try
            {
                await TrainingExerciseService.AddTrainerEntryAsync(
                    _selectedExercise.Id,
                    new AddTrainingTrainerEntryRequest(ev.EventType, ev.Text, string.Empty, ev.TeamName,
                        DateTime.UtcNow, true, "AutoScheduler", "Trainer"),
                    CancellationToken.None);

                await EinsatzService.AddGlobalNoteWithSourceAsync(
                    $"[Trainer-{ev.EventType}] {ev.Text}",
                    "trainer", "Trainer", "Funk",
                    GlobalNotesEntryType.Manual, "Trainer");

                await TrainingExerciseService.MarkScheduledEventFiredAsync(_selectedExercise.Id, ev.Id, CancellationToken.None);
            }
            catch { /* Einzelnes Event-Fehler nicht den gesamten Polling-Lauf stoppen */ }
        }
    }

    // ── Eskalationsstufen ─────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> EscalationMessages = new Dictionary<int, IReadOnlyList<string>>
    {
        [1] = [
            "LAGEMELDUNG: Suche wird auf angrenzende Sektoren ausgeweitet — zusaetzliche Kraefte werden angefordert.",
            "Fuehrungsassistent: Kraeftenachweis sofort aktualisieren und Einsatzleiter vorlegen.",
            "Einsatzleiter: Ressourcenplanung anpassen, Abloesung einplanen."
        ],
        [2] = [
            "ACHTUNG ALLE TEAMS: Lage hat sich verschaerft — medizinische Situation moeglich.",
            "Einsatzleiter: Rettungsdienst alarmieren, Hubschrauberlandeplatz einrichten und absichern.",
            "Alle Teams: Melderhythmus auf 10 Minuten, Kanaele auf Rufbereitschaft halten.",
            "Fuehrungsassistent: Einsatzdokumentation sichern, Leitstelle ueber Lageaenderung informieren."
        ],
        [3] = [
            "VOLLALARM: Grosseinsatz ausgerufen — alle verfuegbaren Kraefte sofort zum ELW.",
            "Einsatzleiter: Ueberortliche Fuehrungsunterstuetzung angefordert, Nachbareinheiten alarmiert.",
            "Fuehrungsassistent: Alarmprotokoll aktivieren, lueckenlose Dokumentation sicherstellen.",
            "Koordination: Abschnittsfuehrung einrichten, Kraefte in Sektoren aufteilen und melden."
        ]
    };

    private async Task SetEscalationLevelAsync(int level)
    {
        if (_selectedExercise is null) return;
        _busy = true;
        try
        {
            var oldLevel = _selectedExercise.EscalationLevel;
            await TrainingExerciseService.SetEscalationLevelAsync(
                _selectedExercise.Id,
                new SetEscalationLevelRequest(level, "Trainer"),
                CancellationToken.None);

            if (level > 0 && level != oldLevel && EscalationMessages.TryGetValue(level, out var messages))
            {
                foreach (var msg in messages)
                {
                    await EinsatzService.AddGlobalNoteWithSourceAsync(
                        $"[Eskalation Stufe {level}] {msg}",
                        "trainer", "Trainer", "Funk",
                        GlobalNotesEntryType.Manual, "Trainer");

                    await TrainingExerciseService.AddTrainerEntryAsync(
                        _selectedExercise.Id,
                        new AddTrainingTrainerEntryRequest("eskalation", msg, string.Empty, string.Empty,
                            DateTime.UtcNow, true, "TrainerPortal", "Trainer"),
                        CancellationToken.None);
                }
            }

            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus(level == 0 ? "Eskalation zurueckgesetzt." : $"Eskalationsstufe {level} aktiviert — {(EscalationMessages.TryGetValue(level, out var m) ? m.Count : 0)} Funksprueche eingespeist.", false);
        }
        catch (Exception ex) { SetStatus($"Fehler: {ex.Message}", true); }
        finally { _busy = false; }
    }

    // ── Stressoren ────────────────────────────────────────────────────────────

    private async Task FireStressorAsync(string text)
    {
        if (_selectedExercise is null) return;
        _busy = true;
        try
        {
            await EinsatzService.AddGlobalNoteWithSourceAsync(
                $"[Stressor] {text}",
                "trainer", "Trainer", "Funk",
                GlobalNotesEntryType.Manual, "Trainer");

            await TrainingExerciseService.AddTrainerEntryAsync(
                _selectedExercise.Id,
                new AddTrainingTrainerEntryRequest("stressor", text, string.Empty, string.Empty,
                    DateTime.UtcNow, true, "TrainerPortal", "Trainer"),
                CancellationToken.None);

            SetStatus($"Stressor eingespeist: {text[..Math.Min(40, text.Length)]}...", false);
        }
        catch (Exception ex) { SetStatus($"Fehler: {ex.Message}", true); }
        finally { _busy = false; }
    }

    private void ShowEndExerciseConfirm()    {
        _endSummary = string.Empty;
        _showEndExerciseConfirm = true;
    }

    private void CancelEndExercise()
    {
        _showEndExerciseConfirm = false;
    }

    private async Task ConfirmEndExerciseAsync()
    {
        if (_selectedExercise is null)
        {
            return;
        }

        _busy = true;
        try
        {
            var exerciseName = _selectedExercise.Name;
            var summary = string.IsNullOrWhiteSpace(_endSummary) ? "Uebung planmaessig beendet." : _endSummary.Trim();

            await TrainingExerciseService.CompleteExerciseAsync(
                _selectedExercise.Id,
                new CompleteTrainingExerciseRequest(
                    Summary: summary,
                    CompletedAtUtc: DateTime.UtcNow,
                    IsTraining: true,
                    SourceSystem: "TrainerPortal",
                    SourceUser: "Trainer"),
                CancellationToken.None);

            // Laufenden Einsatz archivieren, falls einer aktiv ist
            var currentEinsatz = EinsatzService.CurrentEinsatz;
            if (!string.IsNullOrWhiteSpace(currentEinsatz.Einsatzort) && currentEinsatz.EinsatzEnde is null)
            {
                try
                {
                    await ArchivService.ArchiveEinsatzAsync(
                        currentEinsatz,
                        ergebnis: $"Uebungsende: {summary}",
                        bemerkungen: $"Trainer-Uebung '{exerciseName}' beendet.",
                        personalVorOrt: null,
                        hundeVorOrt: null);
                }
                catch
                {
                    // Archivierung optional – Hauptfluss nicht unterbrechen
                }
            }

            // Benachrichtigung an alle Browser-Fenster
            TrainerNotifications.FireExerciseEnded(exerciseName, summary);

            _showEndExerciseConfirm = false;
            await ReloadExercisesAsync();
            _selectedExercise = _exercises.FirstOrDefault(x => x.Id == _selectedExerciseId);
            SetStatus($"Uebung '{exerciseName}' wurde beendet.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Uebung konnte nicht beendet werden: {ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// Klickbare Rueckmeldungsvorschlaege auf Basis der laufenden Teams und der gewaehlten Lagevorlage.
    /// </summary>
    private List<FeedbackSuggestion> GetFeedbackSuggestions()
    {
        var result = new List<FeedbackSuggestion>();

        // Generische Vorschlaege passend zur Lage
        var scenario = SelectedScenario;
        result.Add(new FeedbackSuggestion("lage", "Lage", string.Empty,
            $"Neue Information: Im Bereich {scenario.Title} wurde eine moegliche Spur entdeckt."));
        result.Add(new FeedbackSuggestion("lage", "Lage", string.Empty,
            "Wetterbedingungen haben sich geaendert: Sichtweite eingeschraenkt, Teamleiter informieren."));
        result.Add(new FeedbackSuggestion("funk", "Funk", string.Empty,
            "ELW an alle Teams: Suche wird auf Abschnitt B2 erweitert, bitte einruecken."));
        result.Add(new FeedbackSuggestion("funk", "Funk", string.Empty,
            "Kontrollpunkt Ost meldet: Keine Sichtung, Weitermarsch nach Nord."));

        // Team-spezifische Vorschlaege fuer live angelegte Teams
        foreach (var team in LiveTeams.Take(4))
        {
            result.Add(new FeedbackSuggestion("feedback", "Feedback", team.TeamName,
                $"{team.TeamName}: Ihr Abschnitt wurde abgesucht. Bitte weiter nach Norden ausweichen."));
            result.Add(new FeedbackSuggestion("feedback", "Feedback", team.TeamName,
                $"{team.TeamName}: Guter Einsatz – Dokumentation nicht vergessen."));
        }

        // Team-spezifische Vorschlaege fuer Simulations-Teams
        foreach (var simTeam in scenario.SimulatedTeams.Take(3))
        {
            result.Add(new FeedbackSuggestion("feedback", "Feedback", simTeam,
                $"{simTeam}: Lageupdate – Suchgebiet auf Koordinaten Q3 eingrenzen."));
        }

        return result.Take(12).ToList();
    }

    private void ApplyFeedbackSuggestion(string text, string type, string teamName)
    {
        _entryText = text;
        _entryType = type;
        if (!string.IsNullOrWhiteSpace(teamName))
        {
            _entryParticipantName = teamName;
        }
    }

    private List<TimelineLine> BuildTimeline(TrainingExerciseRecord exercise)
    {
        var lines = new List<TimelineLine>();

        lines.AddRange(exercise.TrainerEntries.Select(entry =>
            new TimelineLine(entry.TimestampUtc, entry.EntryType, entry.Text, entry.ParticipantName)));

        lines.AddRange(exercise.Decisions.Select(decision =>
            new TimelineLine(
                decision.TimestampUtc,
                $"entscheidung:{decision.Category}",
                string.IsNullOrWhiteSpace(decision.Rationale)
                    ? decision.Decision
                    : $"{decision.Decision} | Begruendung: {decision.Rationale}",
                decision.SourceUser)));

        lines.AddRange(exercise.Reports.Select(report =>
            new TimelineLine(report.TimestampUtc, "report", $"{report.Title}: {report.Content}", report.SourceUser)));

        return lines
            .OrderByDescending(x => x.TimestampUtc)
            .Take(60)
            .ToList();
    }

    private void SetStatus(string text, bool isError)
    {
        _statusMessage = text;
        _statusError = isError;
    }

    private static string BuildStartBriefing(ScenarioTemplate scenario)
    {
        var lines = new List<string>
        {
            $"Trainer-Lage ({scenario.Category})",
            scenario.Briefing,
            string.Empty,
            "Entscheidungspunkte fuer Teilnehmer:"
        };

        lines.AddRange(scenario.DecisionPrompts.Select(prompt => $"- {prompt}"));
        return string.Join(Environment.NewLine, lines);
    }

    private void OnTeamAdded(Team team)
    {
        AddTeamJoinReaction(team);
        RefreshLiveComms();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnTeamUpdated(Team team)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnTeamRemoved(Team team)
    {
        _teamJoinReactions.RemoveAll(r => string.Equals(r.TeamName, team.TeamName, StringComparison.OrdinalIgnoreCase));
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnNoteAdded(GlobalNotesEntry note)
    {
        RefreshLiveComms();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnEinsatzChanged()
    {
        RefreshLiveComms();
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        _uiTimer?.Dispose();
        EinsatzService.TeamAdded -= OnTeamAdded;
        EinsatzService.TeamUpdated -= OnTeamUpdated;
        EinsatzService.TeamRemoved -= OnTeamRemoved;
        EinsatzService.EinsatzChanged -= OnEinsatzChanged;
        EinsatzService.NoteAdded -= OnNoteAdded;
    }

    private sealed record FeedbackSuggestion(string Type, string TypeLabel, string TeamName, string Text);
    private sealed record StressorCategory(string Category, IReadOnlyList<string> Items);
    private sealed record QuickTrainerReaction(string TeamName, string Text);

    private static string GetTimelineKindBadge(string kind) => kind switch
    {
        "lage" => "text-bg-info",
        "funk" => "text-bg-primary",
        "feedback" => "text-bg-success",
        "stressor" => "text-bg-danger",
        "eskalation" => "text-bg-warning text-dark",
        _ when kind.StartsWith("entscheidung") => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    private sealed record ScenarioTemplate(
        string Id,
        string Title,
        string Category,
        string Briefing,
        IReadOnlyList<string> DecisionPrompts,
        IReadOnlyList<string> SimulatedTeams);

    private sealed record TimelineLine(
        DateTime TimestampUtc,
        string Kind,
        string Text,
        string Source)
    {
        public string TimeLabel => TimestampUtc.ToLocalTime().ToString("HH:mm");
    }

    private sealed class TrainerAuthStatusDto
    {
        public bool Authenticated { get; set; }
    }
}
