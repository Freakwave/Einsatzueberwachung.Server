using System.Text;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Services;
using Einsatzueberwachung.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Einsatzueberwachung.Tests;

public class GpxImportControllerTests
{
    [Fact]
    public async Task Import_AcceptsEnumNameHumanTrack()
    {
        var einsatz = new EinsatzService();
        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Alpha" });
        var controller = CreateController(einsatz);

        var result = await controller.Import(
            CreateGpxFile(),
            team.TeamId,
            nameof(TrackType.HumanTrack),
            searchStart: new DateTime(2026, 5, 8, 8, 0, 0),
            searchEnd: new DateTime(2026, 5, 8, 9, 0, 0));

        var ok = Assert.IsType<OkObjectResult>(result);
        var snapshot = Assert.IsType<TeamTrackSnapshot>(ok.Value);
        Assert.Equal(TrackType.HumanTrack, snapshot.TrackType);
    }

    [Fact]
    public async Task Import_AcceptsLegacyHumanLiteral()
    {
        var einsatz = new EinsatzService();
        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Bravo" });
        var controller = CreateController(einsatz);

        var result = await controller.Import(
            CreateGpxFile(),
            team.TeamId,
            "human",
            searchStart: new DateTime(2026, 5, 8, 8, 0, 0),
            searchEnd: new DateTime(2026, 5, 8, 9, 0, 0));

        var ok = Assert.IsType<OkObjectResult>(result);
        var snapshot = Assert.IsType<TeamTrackSnapshot>(ok.Value);
        Assert.Equal(TrackType.HumanTrack, snapshot.TrackType);
    }

    [Fact]
    public async Task Import_InvalidTrackType_ReturnsBadRequest()
    {
        var einsatz = new EinsatzService();
        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Charlie" });
        var controller = CreateController(einsatz);

        var result = await controller.Import(
            CreateGpxFile(),
            team.TeamId,
            "not-a-track-type",
            searchStart: new DateTime(2026, 5, 8, 8, 0, 0),
            searchEnd: new DateTime(2026, 5, 8, 9, 0, 0));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static GpxImportController CreateController(EinsatzService einsatz)
    {
        return new GpxImportController(
            einsatz,
            new FixedTimeService(new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Local)),
            NullLogger<GpxImportController>.Instance);
    }

    private static IFormFile CreateGpxFile()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.1" lon="11.5"><time>2026-05-08T08:00:00Z</time></trkpt>
                <trkpt lat="48.2" lon="11.6"><time>2026-05-08T08:05:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var bytes = Encoding.UTF8.GetBytes(gpx);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "track.gpx");
    }

    private sealed class FixedTimeService(DateTime now) : ITimeService
    {
        public DateTime Now => now;

        public void Refresh()
        {
        }
    }
}