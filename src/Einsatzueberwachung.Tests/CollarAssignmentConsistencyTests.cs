using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Tests;

public class CollarAssignmentConsistencyTests
{
    [Fact]
    public async Task OutOfBoundsWarning_DoesNotFireWhenTeamNotRunning()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Team OOB" });
        var area = await einsatz.AddSearchAreaAsync(CreateSquareArea("Area OOB"));
        await einsatz.AssignTeamToSearchAreaAsync(area.Id, team.TeamId);
        await collars.AssignCollarToTeamAsync("C-OOB", team.TeamId);

        var fired = false;
        collars.OutOfBoundsDetected += (_, _, _) => fired = true;

        await collars.ReceiveLocationAsync("C-OOB", "Collar OOB", 49.02, 8.02, batteryLevel: 3);

        Assert.False(fired);
    }

    [Fact]
    public async Task OutOfBoundsWarning_FiresWhenTeamRunning()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Team OOB Run" });
        var area = await einsatz.AddSearchAreaAsync(CreateSquareArea("Area OOB Run"));
        await einsatz.AssignTeamToSearchAreaAsync(area.Id, team.TeamId);
        await collars.AssignCollarToTeamAsync("C-OOB-RUN", team.TeamId);
        await einsatz.StartTeamTimerAsync(team.TeamId);

        var fired = false;
        collars.OutOfBoundsDetected += (_, _, _) => fired = true;

        await collars.ReceiveLocationAsync("C-OOB-RUN", "Collar OOB Run", 49.02, 8.02, batteryLevel: 3);

        Assert.True(fired);
    }

    [Fact]
    public async Task ReceiveLocation_UpdatesLatestLocationEvenWhenTeamNotRunning()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Team Live" });
        await collars.AssignCollarToTeamAsync("C-LIVE", team.TeamId);

        await collars.ReceiveLocationAsync("C-LIVE", "Live Collar", 49.12345, 8.54321, batteryLevel: 2);

        var latest = collars.GetLatestLocation("C-LIVE");
        var history = collars.GetLocationHistory("C-LIVE");

        Assert.NotNull(latest);
        Assert.Equal(49.12345, latest!.Latitude, 5);
        Assert.Equal(8.54321, latest.Longitude, 5);
        Assert.Empty(history);
    }

    [Fact]
    public async Task AssignCollarToTeam_ReleasesPreviousCollarFromTargetTeam()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var teamA = await einsatz.AddTeamAsync(new Team { TeamName = "Team A" });
        var teamB = await einsatz.AddTeamAsync(new Team { TeamName = "Team B" });

        await collars.AssignCollarToTeamAsync("C-1", teamA.TeamId);
        await collars.AssignCollarToTeamAsync("C-2", teamB.TeamId);

        await collars.AssignCollarToTeamAsync("C-1", teamB.TeamId);

        var c1 = collars.Collars.Single(c => c.Id == "C-1");
        var c2 = collars.Collars.Single(c => c.Id == "C-2");

        Assert.Equal("C-1", teamB.CollarId);
        Assert.Null(teamA.CollarId);

        Assert.True(c1.IsAssigned);
        Assert.Equal(teamB.TeamId, c1.AssignedTeamId);

        Assert.False(c2.IsAssigned);
        Assert.Null(c2.AssignedTeamId);
    }

    [Fact]
    public async Task RemoveTeam_UnassignsCollarInTrackingRegistry()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Team C" });
        await collars.AssignCollarToTeamAsync("C-3", team.TeamId);

        await einsatz.RemoveTeamAsync(team.TeamId);

        var collar = collars.Collars.Single(c => c.Id == "C-3");
        Assert.False(collar.IsAssigned);
        Assert.Null(collar.AssignedTeamId);
    }

    [Fact]
    public async Task AssignUnknownCollar_CreatesPlaceholderAndAssigns()
    {
        var einsatz = new EinsatzService();
        var collars = new CollarTrackingService(einsatz);

        var team = await einsatz.AddTeamAsync(new Team { TeamName = "Team D" });

        await collars.AssignCollarToTeamAsync("C-RESTORE", team.TeamId);

        var collar = collars.Collars.Single(c => c.Id == "C-RESTORE");

        Assert.True(collar.IsAssigned);
        Assert.Equal(team.TeamId, collar.AssignedTeamId);
        Assert.Equal("C-RESTORE", team.CollarId);
        Assert.False(string.IsNullOrWhiteSpace(team.CollarName));
    }

    private static SearchArea CreateSquareArea(string name)
    {
        return new SearchArea
        {
            Name = name,
            Coordinates = new List<(double Latitude, double Longitude)>
            {
                (49.00, 8.00),
                (49.00, 8.01),
                (49.01, 8.01),
                (49.01, 8.00),
                (49.00, 8.00)
            }
        };
    }
}
