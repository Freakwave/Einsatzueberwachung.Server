using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Tests;

public class CompletedSearchTrackImportTests
{
    [Fact]
    public async Task AddTrackToCompletedSearchAsync_UsesTargetSearchAreaMetadata()
    {
        var einsatz = new EinsatzService();
        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Alpha" });
        var currentArea = await einsatz.AddSearchAreaAsync(CreateSquareArea("Aktuelles Gebiet", "#1188AA"));
        var importedArea = await einsatz.AddSearchAreaAsync(CreateSquareArea("Importiertes Gebiet", "#CC5500"));

        await einsatz.AssignTeamToSearchAreaAsync(currentArea.Id, team.TeamId);

        var search = await einsatz.CreateCompletedSearchAsync(
            team.TeamId,
            new DateTime(2026, 5, 8, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc),
            importedArea.Id);

        var snapshot = new TeamTrackSnapshot
        {
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            SearchAreaName = team.SearchAreaName,
            Color = "#123456",
            TrackType = TrackType.HumanTrack,
            Points =
            [
                new TrackPoint { Latitude = 48.1, Longitude = 11.5, Timestamp = new DateTime(2026, 5, 8, 8, 0, 0, DateTimeKind.Utc) },
                new TrackPoint { Latitude = 48.2, Longitude = 11.6, Timestamp = new DateTime(2026, 5, 8, 8, 30, 0, DateTimeKind.Utc) }
            ]
        };

        await einsatz.AddTrackToCompletedSearchAsync(search.Id, snapshot);

        Assert.Equal("Importiertes Gebiet", snapshot.SearchAreaName);
        Assert.Equal("#CC5500", snapshot.SearchAreaColor);
        Assert.Equal(importedArea.Coordinates, snapshot.SearchAreaCoordinates);
    }

    [Fact]
    public async Task AddTrackToCompletedSearchAsync_ClearsStaleSearchAreaMetadataWhenTargetHasNoArea()
    {
        var einsatz = new EinsatzService();
        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Bravo" });
        var currentArea = await einsatz.AddSearchAreaAsync(CreateSquareArea("Aktuelles Gebiet", "#1188AA"));

        await einsatz.AssignTeamToSearchAreaAsync(currentArea.Id, team.TeamId);

        var search = await einsatz.CreateCompletedSearchAsync(
            team.TeamId,
            new DateTime(2026, 5, 8, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 8, 11, 0, 0, DateTimeKind.Utc));

        var snapshot = new TeamTrackSnapshot
        {
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            SearchAreaName = team.SearchAreaName,
            SearchAreaColor = currentArea.Color,
            SearchAreaCoordinates = new List<(double Latitude, double Longitude)>(currentArea.Coordinates),
            TrackType = TrackType.CollarTrack,
            Points =
            [
                new TrackPoint { Latitude = 48.1, Longitude = 11.5, Timestamp = new DateTime(2026, 5, 8, 10, 0, 0, DateTimeKind.Utc) },
                new TrackPoint { Latitude = 48.2, Longitude = 11.6, Timestamp = new DateTime(2026, 5, 8, 10, 30, 0, DateTimeKind.Utc) }
            ]
        };

        await einsatz.AddTrackToCompletedSearchAsync(search.Id, snapshot);

        Assert.Equal(string.Empty, snapshot.SearchAreaName);
        Assert.Equal(string.Empty, snapshot.SearchAreaColor);
        Assert.Empty(snapshot.SearchAreaCoordinates);
    }

    private static SearchArea CreateSquareArea(string name, string color)
    {
        return new SearchArea
        {
            Name = name,
            Color = color,
            Coordinates =
            [
                (49.00, 8.00),
                (49.00, 8.01),
                (49.01, 8.01),
                (49.01, 8.00),
                (49.00, 8.00)
            ]
        };
    }
}