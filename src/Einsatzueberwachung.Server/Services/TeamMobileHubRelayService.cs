using System.Text.Json;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Einsatzueberwachung.Server.Services;

/// <summary>
/// Forwarded GPS-Updates und Lifecycle-Events nur an die jeweils berechtigte Team-Gruppe.
/// </summary>
public sealed class TeamMobileHubRelayService : IHostedService
{
    private readonly ICollarTrackingService _collarTrackingService;
    private readonly IEinsatzService _einsatzService;
    private readonly ITeamMobileTokenService _tokenService;
    private readonly IHubContext<TeamMobileHub> _hubContext;
    private readonly ILogger<TeamMobileHubRelayService> _logger;

    public TeamMobileHubRelayService(
        ICollarTrackingService collarTrackingService,
        IEinsatzService einsatzService,
        ITeamMobileTokenService tokenService,
        IHubContext<TeamMobileHub> hubContext,
        ILogger<TeamMobileHubRelayService> logger)
    {
        _collarTrackingService = collarTrackingService;
        _einsatzService = einsatzService;
        _tokenService = tokenService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _collarTrackingService.CollarLocationReceived += OnCollarLocationReceived;
        _collarTrackingService.OutOfBoundsDetected += OnOutOfBoundsDetected;
        _einsatzService.TeamUpdated += OnTeamUpdated;
        _tokenService.GenerationChanged += OnGenerationChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _collarTrackingService.CollarLocationReceived -= OnCollarLocationReceived;
        _collarTrackingService.OutOfBoundsDetected -= OnOutOfBoundsDetected;
        _einsatzService.TeamUpdated -= OnTeamUpdated;
        _tokenService.GenerationChanged -= OnGenerationChanged;
        return Task.CompletedTask;
    }

    private void OnCollarLocationReceived(string collarId, CollarLocation location)
    {
        var team = _einsatzService.Teams.FirstOrDefault(t => t.CollarId == collarId);
        if (team == null) return;

        _ = PublishToTeamAsync(team.TeamId, "collar.location", new
        {
            lat = location.Latitude,
            lng = location.Longitude,
            timestamp = location.Timestamp
        });
    }

    private void OnOutOfBoundsDetected(string teamId, string collarId, CollarLocation location)
    {
        _ = PublishToTeamAsync(teamId, "collar.outofbounds", new
        {
            lat = location.Latitude,
            lng = location.Longitude,
            timestamp = location.Timestamp
        });
    }

    private void OnTeamUpdated(Team team)
    {
        _ = PublishToTeamAsync(team.TeamId, "team.updated", new
        {
            teamId = team.TeamId,
            isRunning = team.IsRunning,
            collarId = team.CollarId,
            searchAreaId = team.SearchAreaId
        });
    }

    private void OnGenerationChanged()
    {
        // Globaler Broadcast: jeder verbundene Team-Client soll seine Session beenden.
        _ = _hubContext.Clients.All.SendAsync(TeamMobileHub.EventName, "session.invalidated", "{}");
        _logger.LogInformation("TeamMobile-Generation gewechselt – alle Sessions invalidiert.");
    }

    private Task PublishToTeamAsync(string teamId, string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return _hubContext.Clients
            .Group(TeamMobileHub.TeamGroup(teamId))
            .SendAsync(TeamMobileHub.EventName, eventName, json);
    }
}
