using Einsatzueberwachung.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Einsatzueberwachung.Server.Hubs;

[Authorize(Policy = TeamMobileAuth.AuthorizationPolicy, AuthenticationSchemes = TeamMobileAuth.AuthenticationScheme)]
public sealed class TeamMobileHub : Hub
{
    public const string EventName = "team-mobile:update";

    public static string TeamGroup(string teamId) => $"team-mobile-{teamId}";

    public override async Task OnConnectedAsync()
    {
        var teamId = Context.User?.FindFirst(TeamMobileAuth.TeamIdClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(teamId))
            await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var teamId = Context.User?.FindFirst(TeamMobileAuth.TeamIdClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(teamId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(teamId));

        await base.OnDisconnectedAsync(exception);
    }
}
