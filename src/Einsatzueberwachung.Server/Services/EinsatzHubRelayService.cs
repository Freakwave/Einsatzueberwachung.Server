using System.Text.Json;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Einsatzueberwachung.Server.Services;

public sealed class EinsatzHubRelayService : IHostedService
{
    private readonly IEinsatzService _einsatzService;
    private readonly IHubContext<EinsatzHub> _hubContext;

    public EinsatzHubRelayService(IEinsatzService einsatzService, IHubContext<EinsatzHub> hubContext)
    {
        _einsatzService = einsatzService;
        _hubContext = hubContext;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _einsatzService.EinsatzChanged += OnEinsatzChanged;
        _einsatzService.TeamAdded += OnTeamAdded;
        _einsatzService.TeamRemoved += OnTeamRemoved;
        _einsatzService.TeamUpdated += OnTeamUpdated;
        _einsatzService.NoteAdded += OnNoteAdded;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _einsatzService.EinsatzChanged -= OnEinsatzChanged;
        _einsatzService.TeamAdded -= OnTeamAdded;
        _einsatzService.TeamRemoved -= OnTeamRemoved;
        _einsatzService.TeamUpdated -= OnTeamUpdated;
        _einsatzService.NoteAdded -= OnNoteAdded;

        return Task.CompletedTask;
    }

    private void OnEinsatzChanged()
    {
        _ = PublishAsync("einsatz.changed", new
        {
            einsatz = _einsatzService.CurrentEinsatz,
            teams = _einsatzService.Teams.Count,
            notes = _einsatzService.GlobalNotes.Count
        });
    }

    private void OnTeamAdded(Team team) => _ = PublishAsync("team.added", team);

    private void OnTeamRemoved(Team team) => _ = PublishAsync("team.removed", team);

    private void OnTeamUpdated(Team team) => _ = PublishAsync("team.updated", team);

    private void OnNoteAdded(GlobalNotesEntry note) => _ = PublishAsync("note.added", note);

    private Task PublishAsync(string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return _hubContext.Clients.All.SendAsync("einsatz:update", eventName, json);
    }
}
