using System.Security.Claims;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Server.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einsatzueberwachung.Server.Controllers;

[ApiController]
[Route("api/team-mobile")]
[IgnoreAntiforgeryToken]
public sealed class TeamMobileController : ControllerBase
{
    private readonly ITeamMobileTokenService _tokenService;
    private readonly IEinsatzService _einsatzService;
    private readonly ICollarTrackingService _collarTrackingService;
    private readonly ILogger<TeamMobileController> _logger;

    public TeamMobileController(
        ITeamMobileTokenService tokenService,
        IEinsatzService einsatzService,
        ICollarTrackingService collarTrackingService,
        ILogger<TeamMobileController> logger)
    {
        _tokenService = tokenService;
        _einsatzService = einsatzService;
        _collarTrackingService = collarTrackingService;
        _logger = logger;
    }

    [HttpGet("teams")]
    public IActionResult ListTeams([FromQuery] string master)
    {
        if (!_tokenService.ValidateMasterToken(master))
            return Unauthorized(new { error = "Ungueltiger Master-Token oder kein aktiver Einsatz." });

        var einsatz = _einsatzService.CurrentEinsatz;
        if (einsatz is not { IstEinsatz: true } || einsatz.EinsatzEnde != null)
            return NotFound(new { error = "Kein aktiver Einsatz." });

        var teams = _einsatzService.Teams
            .OrderBy(t => t.TeamName)
            .Select(t => new
            {
                teamId = t.TeamId,
                teamName = t.TeamName,
                dogName = t.DogName,
                hundefuehrerName = t.HundefuehrerName,
                hasCollar = !string.IsNullOrWhiteSpace(t.CollarId),
                hasSearchArea = !string.IsNullOrWhiteSpace(t.SearchAreaId)
            })
            .ToList();

        return Ok(new
        {
            einsatzort = einsatz.Einsatzort,
            stichwort = einsatz.Stichwort,
            teams
        });
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectTeam([FromBody] TeamMobileSelectRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MasterToken) || string.IsNullOrWhiteSpace(request.TeamId))
            return BadRequest(new { error = "MasterToken und TeamId erforderlich." });

        if (!_tokenService.ValidateMasterToken(request.MasterToken))
            return Unauthorized(new { error = "Ungueltiger Master-Token." });

        var team = _einsatzService.Teams.FirstOrDefault(t => t.TeamId == request.TeamId);
        if (team == null)
            return NotFound(new { error = "Team nicht gefunden." });

        var cookieValue = _tokenService.IssueTeamCookieValue(team.TeamId);

        var claims = new[]
        {
            new Claim(TeamMobileAuth.TeamIdClaim, team.TeamId),
            new Claim(TeamMobileAuth.GenerationClaim, _tokenService.CurrentGeneration.ToString()),
            new Claim("team-cookie", cookieValue)
        };
        var identity = new ClaimsIdentity(claims, TeamMobileAuth.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            TeamMobileAuth.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
            });

        _logger.LogInformation("TeamMobile-Login fuer Team {TeamName} ({TeamId})", team.TeamName, team.TeamId);
        return Ok(new { teamId = team.TeamId, teamName = team.TeamName });
    }

    [HttpPost("select-form")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> SelectTeamForm([FromForm] string master, [FromForm] string teamId)
    {
        var result = await SelectTeam(new TeamMobileSelectRequest { MasterToken = master, TeamId = teamId });
        if (result is OkObjectResult)
            return Redirect("/team");
        return result;
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(TeamMobileAuth.AuthenticationScheme);
        return Redirect("/team/login");
    }

    [HttpGet("state")]
    [Authorize(Policy = TeamMobileAuth.AuthorizationPolicy)]
    public IActionResult GetState()
    {
        var teamId = User.FindFirst(TeamMobileAuth.TeamIdClaim)?.Value;
        if (string.IsNullOrWhiteSpace(teamId)) return Unauthorized();

        var einsatz = _einsatzService.CurrentEinsatz;
        var einsatzActive = einsatz is { IstEinsatz: true } && einsatz.EinsatzEnde == null;

        var team = _einsatzService.Teams.FirstOrDefault(t => t.TeamId == teamId);
        if (team == null)
            return NotFound(new { error = "Team nicht mehr vorhanden.", einsatzActive });

        SearchArea? searchArea = null;
        if (!string.IsNullOrWhiteSpace(team.SearchAreaId) && einsatz?.SearchAreas != null)
            searchArea = einsatz.SearchAreas.FirstOrDefault(a => a.Id == team.SearchAreaId);

        IReadOnlyList<CollarLocation> trackPoints = Array.Empty<CollarLocation>();
        CollarLocation? lastLocation = null;
        if (!string.IsNullOrWhiteSpace(team.CollarId))
        {
            trackPoints = _collarTrackingService.GetLocationHistory(team.CollarId);
            lastLocation = trackPoints.Count > 0 ? trackPoints[^1] : null;
        }

        return Ok(new
        {
            einsatzActive,
            team = new
            {
                teamId = team.TeamId,
                teamName = team.TeamName,
                dogName = team.DogName,
                hundefuehrerName = team.HundefuehrerName,
                helferName = team.HelferName,
                isRunning = team.IsRunning,
                collarId = team.CollarId,
                collarName = team.CollarName
            },
            searchArea = searchArea == null ? null : new
            {
                id = searchArea.Id,
                name = searchArea.Name,
                color = searchArea.Color,
                coordinates = searchArea.Coordinates?
                    .Select(c => new { lat = c.Latitude, lng = c.Longitude })
                    .ToList()
            },
            lastLocation = lastLocation == null ? null : new
            {
                lat = lastLocation.Latitude,
                lng = lastLocation.Longitude,
                timestamp = lastLocation.Timestamp
            },
            track = trackPoints
                .Select(p => new { lat = p.Latitude, lng = p.Longitude, timestamp = p.Timestamp })
                .ToList()
        });
    }
}

public sealed class TeamMobileSelectRequest
{
    public string MasterToken { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
}
