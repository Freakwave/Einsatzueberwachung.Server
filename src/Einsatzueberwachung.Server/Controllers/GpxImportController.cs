using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Services;
using Einsatzueberwachung.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace Einsatzueberwachung.Server.Controllers;

/// <summary>
/// Nimmt GPX-Dateien entgegen und importiert sie als abgeschlossene Suchen in den laufenden Einsatz.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GpxImportController : ControllerBase
{
    private readonly IEinsatzService _einsatzService;
    private readonly ITimeService _timeService;
    private readonly ILogger<GpxImportController> _logger;

    private static readonly string[] TrackColors =
    [
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8",
        "#F7DC6F", "#BB8FCE", "#85C1E2", "#FF8800", "#44FF88"
    ];

    public GpxImportController(
        IEinsatzService einsatzService,
        ITimeService timeService,
        ILogger<GpxImportController> logger)
    {
        _einsatzService = einsatzService;
        _timeService = timeService;
        _logger = logger;
    }

    /// <summary>
    /// Liest eine GPX-Datei und gibt alle Trackpunkte zurück (ohne Speichern).
    /// Dient der UI-Vorschau mit Zeitfenster-Auswahl.
    /// </summary>
    [HttpPost("preview")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Preview(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse("Keine Datei angegeben."));

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("Nur GPX-Dateien (.gpx) werden unterstützt."));

        try
        {
            List<TrackPoint> points;
            await using (var stream = file.OpenReadStream())
                points = await GpxParser.ParseAsync(stream);

            return Ok(new GpxPreviewResponse(
                points.Count,
                points.FirstOrDefault()?.Timestamp,
                points.LastOrDefault()?.Timestamp,
                points.Select(p => new GpxPointDto(p.Latitude, p.Longitude, p.Timestamp)).ToList()
            ));
        }
        catch (FormatException ex)
        {
            return BadRequest(new ErrorResponse($"GPX-Datei konnte nicht gelesen werden: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Lesen der GPX-Datei {FileName}", file.FileName);
            return StatusCode(500, new ErrorResponse("Interner Fehler beim Verarbeiten der GPX-Datei."));
        }
    }

    /// <summary>
    /// Importiert eine GPX-Datei als abgeschlossene Suche und fügt sie dem laufenden Einsatz hinzu.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromForm] string teamId,
        [FromForm] string trackType = "collar",
        [FromForm] DateTime? startTime = null,
        [FromForm] DateTime? endTime = null,
        [FromForm] string? color = null,
        [FromForm] string? completedSearchId = null,
        [FromForm] DateTime? searchStart = null,
        [FromForm] DateTime? searchEnd = null,
        [FromForm] string? searchAreaId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ErrorResponse("Keine Datei angegeben."));

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("Nur GPX-Dateien (.gpx) werden unterstützt."));

        if (string.IsNullOrWhiteSpace(teamId))
            return BadRequest(new ErrorResponse("TeamId ist erforderlich."));

        var team = _einsatzService.Teams.FirstOrDefault(t => t.TeamId == teamId);
        if (team == null)
            return NotFound(new ErrorResponse($"Team '{teamId}' nicht gefunden."));

        // Wenn keine bestehende Suche angegeben, müssen Start und Ende vorhanden sein
        if (string.IsNullOrWhiteSpace(completedSearchId) && (searchStart == null || searchEnd == null))
            return BadRequest(new ErrorResponse("Ohne completedSearchId sind searchStart und searchEnd erforderlich."));

        var parsedTrackType = string.Equals(trackType, "human", StringComparison.OrdinalIgnoreCase)
            ? TrackType.HumanTrack
            : TrackType.CollarTrack;

        try
        {
            List<TrackPoint> allPoints;
            await using (var stream = file.OpenReadStream())
                allPoints = await GpxParser.ParseAsync(stream);

            // Zeitfenster anwenden (nur für Collar-Tracks explizit verlangt, aber bei beiden möglich)
            var filteredPoints = GpxParser.ApplyTimeFilter(allPoints, startTime, endTime);

            if (filteredPoints.Count == 0)
                return BadRequest(new ErrorResponse("Nach Anwendung des Zeitfensters wurden keine Punkte übernommen."));

            var resolvedColor = color is { Length: > 0 } ? color
                : TrackColors[(teamId.GetHashCode() & 0x7FFFFFFF) % TrackColors.Length];

            var snapshot = new TeamTrackSnapshot
            {
                TeamId = team.TeamId,
                TeamName = team.TeamName,
                SearchAreaName = team.SearchAreaName ?? string.Empty,
                CollarId = parsedTrackType == TrackType.CollarTrack ? (team.CollarId ?? string.Empty) : string.Empty,
                CollarName = parsedTrackType == TrackType.CollarTrack ? (team.CollarName ?? string.Empty) : string.Empty,
                Color = resolvedColor,
                CapturedAt = _timeService.Now,
                TrackType = parsedTrackType,
                ImportedAt = _timeService.Now,
                Points = filteredPoints
            };

            if (!string.IsNullOrWhiteSpace(completedSearchId))
            {
                // In bestehende Suche importieren
                var existingSearches = _einsatzService.CurrentEinsatz.CompletedSearches ?? new();
                var targetSearch = existingSearches.FirstOrDefault(cs => cs.Id == completedSearchId);

                if (targetSearch == null)
                    return NotFound(new ErrorResponse($"CompletedSearch '{completedSearchId}' nicht gefunden."));

                if (targetSearch.TeamId != teamId)
                    return StatusCode(403, new ErrorResponse("Diese Suche gehört nicht zum angegebenen Team."));

                var canAdd = parsedTrackType == TrackType.CollarTrack ? targetSearch.CanAddCollarTrack : targetSearch.CanAddHumanTrack;
                if (!canAdd)
                    return Conflict(new ErrorResponse($"Diese Suche enthält bereits einen Track vom Typ '{parsedTrackType}'."));

                await _einsatzService.AddTrackToCompletedSearchAsync(completedSearchId, snapshot);
            }
            else
            {
                // Neue Suche anlegen und Track hinzufügen
                var newSearch = await _einsatzService.CreateCompletedSearchAsync(
                    teamId, searchStart!.Value, searchEnd!.Value, searchAreaId);
                await _einsatzService.AddTrackToCompletedSearchAsync(newSearch.Id, snapshot);
            }

            _logger.LogInformation(
                "GPX-Import: {PointCount} Punkte als {TrackType} für Team '{TeamName}' importiert.",
                filteredPoints.Count, parsedTrackType, team.TeamName.Replace('\n', ' ').Replace('\r', ' '));

            return Ok(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (FormatException ex)
        {
            return BadRequest(new ErrorResponse($"GPX-Datei konnte nicht gelesen werden: {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim GPX-Import für Team {TeamId}", teamId);
            return StatusCode(500, new ErrorResponse("Interner Fehler beim Importieren der GPX-Datei."));
        }
    }

    public sealed record GpxPreviewResponse(
        int TotalPoints,
        DateTime? FirstTimestamp,
        DateTime? LastTimestamp,
        List<GpxPointDto> Points);

    public sealed record GpxPointDto(double Latitude, double Longitude, DateTime Timestamp);
}
