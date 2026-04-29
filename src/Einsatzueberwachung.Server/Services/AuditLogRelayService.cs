using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Microsoft.Extensions.Hosting;

namespace Einsatzueberwachung.Server.Services
{
    public class AuditLogRelayService : IHostedService
    {
        private readonly IEinsatzService _einsatzService;
        private readonly IAuditLogService _auditLog;

        public AuditLogRelayService(IEinsatzService einsatzService, IAuditLogService auditLog)
        {
            _einsatzService = einsatzService;
            _auditLog = auditLog;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _einsatzService.EinsatzChanged += OnEinsatzChanged;
            _einsatzService.TeamAdded += OnTeamAdded;
            _einsatzService.TeamRemoved += OnTeamRemoved;
            _einsatzService.TeamUpdated += OnTeamUpdated;
            _einsatzService.NoteAdded += OnNoteAdded;
            _einsatzService.TeamWarningTriggered += OnTeamWarning;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _einsatzService.EinsatzChanged -= OnEinsatzChanged;
            _einsatzService.TeamAdded -= OnTeamAdded;
            _einsatzService.TeamRemoved -= OnTeamRemoved;
            _einsatzService.TeamUpdated -= OnTeamUpdated;
            _einsatzService.NoteAdded -= OnNoteAdded;
            _einsatzService.TeamWarningTriggered -= OnTeamWarning;
            return Task.CompletedTask;
        }

        private void OnEinsatzChanged()
        {
            var e = _einsatzService.CurrentEinsatz;
            _auditLog.Log("Einsatz geändert",
                string.IsNullOrWhiteSpace(e.Einsatzort)
                    ? "Einsatzdaten zurückgesetzt"
                    : $"Einsatzort: {e.Einsatzort}, Leiter: {e.Einsatzleiter}");
        }

        private void OnTeamAdded(Team team)
        {
            _auditLog.Log("Team hinzugefügt", $"Typ: {GetTeamTyp(team)}, Suchgebiet: {team.SearchAreaName ?? "—"}", team.TeamName);
        }

        private void OnTeamRemoved(Team team)
        {
            _auditLog.Log("Team entfernt", $"Laufzeit: {team.ElapsedTime:hh\\:mm\\:ss}", team.TeamName);
        }

        private void OnTeamUpdated(Team team)
        {
            string status = team.IsRunning ? "läuft" : team.IsPausing ? "pausiert" : "gestoppt";
            _auditLog.Log("Team aktualisiert", $"Status: {status}, Laufzeit: {team.ElapsedTime:hh\\:mm\\:ss}", team.TeamName);
        }

        private void OnNoteAdded(GlobalNotesEntry note)
        {
            string aktion = note.SourceType == "Funk" ? "Funkspruch erfasst" : "Notiz erfasst";
            _auditLog.Log(aktion, note.Text ?? "", note.SourceTeamName);
        }

        private void OnTeamWarning(Team team, bool isSecond)
        {
            string stufe = isSecond ? "2. Warnung (kritisch)" : "1. Warnung";
            _auditLog.Log("Timer-Warnung", $"{stufe} — Laufzeit: {team.ElapsedTime:hh\\:mm\\:ss}", team.TeamName);
        }

        private static string GetTeamTyp(Team team)
            => team.IsDroneTeam ? "Drohnenteam" : team.IsSupportTeam ? "Unterstützung" : "Hundeteam";
    }
}
