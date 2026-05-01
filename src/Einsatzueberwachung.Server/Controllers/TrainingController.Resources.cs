using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Mvc;

namespace Einsatzueberwachung.Server.Controllers;

public sealed partial class TrainingController
{
    [HttpGet("personnel")]
    public async Task<IActionResult> GetPersonnel(CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var personnel = await _masterDataService.GetPersonalListAsync();
        var response = personnel
            .OrderBy(p => p.Nachname)
            .ThenBy(p => p.Vorname)
            .Select(p => new TrainingPersonnelDto(
                p.Id,
                p.FullName,
                p.SkillsShortDisplay,
                p.IsActive,
                p.IsActive))
            .ToList();

        return Ok(new { snapshotUtc = DateTime.UtcNow, personnel = response, count = response.Count });
    }

    [HttpGet("dogs")]
    public async Task<IActionResult> GetDogs(CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var dogs = await _masterDataService.GetDogListAsync();
        var response = dogs
            .OrderBy(d => d.Name)
            .Select(d => new TrainingDogDto(
                d.Id,
                d.Name,
                d.Rasse,
                d.Alter,
                d.SpecializationsShortDisplay,
                d.IsActive,
                d.IsActive,
                d.HundefuehrerIds))
            .ToList();

        return Ok(new { snapshotUtc = DateTime.UtcNow, dogs = response, count = response.Count });
    }

    [HttpGet("drones")]
    public async Task<IActionResult> GetDrones(CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var drones = await _masterDataService.GetDroneListAsync();
        var response = drones
            .OrderBy(d => d.Name)
            .Select(d => new TrainingDroneDto(
                d.Id,
                d.Name,
                d.Modell,
                d.Hersteller,
                d.DrohnenpilotId,
                d.IsActive,
                d.IsActive))
            .ToList();

        return Ok(new { snapshotUtc = DateTime.UtcNow, drones = response, count = response.Count });
    }

    [HttpGet("teams")]
    public IActionResult GetTeams()
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var teams = _einsatzService.Teams
            .OrderBy(t => t.TeamName)
            .Select(t => new TrainingTeamDto(
                t.TeamId,
                t.TeamName,
                t.DogId,
                t.DogName,
                t.HundefuehrerId,
                t.HundefuehrerName,
                t.IsRunning,
                ResolveTeamStatus(t),
                !t.IsRunning))
            .ToList();

        return Ok(new { snapshotUtc = DateTime.UtcNow, teams, count = teams.Count });
    }

    [HttpGet("resources")]
    public async Task<IActionResult> GetResourcesSnapshot(CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var personnel = await _masterDataService.GetPersonalListAsync();
        var dogs = await _masterDataService.GetDogListAsync();
        var drones = await _masterDataService.GetDroneListAsync();

        var personnelDto = personnel
            .OrderBy(p => p.Nachname)
            .ThenBy(p => p.Vorname)
            .Select(p => new TrainingPersonnelDto(p.Id, p.FullName, p.SkillsShortDisplay, p.IsActive, p.IsActive))
            .ToList();

        var dogDto = dogs
            .OrderBy(d => d.Name)
            .Select(d => new TrainingDogDto(d.Id, d.Name, d.Rasse, d.Alter, d.SpecializationsShortDisplay, d.IsActive, d.IsActive, d.HundefuehrerIds))
            .ToList();

        var droneDto = drones
            .OrderBy(d => d.Name)
            .Select(d => new TrainingDroneDto(d.Id, d.Name, d.Modell, d.Hersteller, d.DrohnenpilotId, d.IsActive, d.IsActive))
            .ToList();

        var teams = _einsatzService.Teams
            .OrderBy(t => t.TeamName)
            .Select(t => new TrainingTeamDto(
                t.TeamId,
                t.TeamName,
                t.DogId,
                t.DogName,
                t.HundefuehrerId,
                t.HundefuehrerName,
                t.IsRunning,
                ResolveTeamStatus(t),
                !t.IsRunning))
            .ToList();

        var snapshot = new TrainingResourceSnapshotDto(
            DateTime.UtcNow,
            personnelDto.Count,
            dogDto.Count,
            droneDto.Count,
            teams.Count,
            personnelDto,
            dogDto,
            droneDto,
            teams);

        return Ok(snapshot);
    }
}
