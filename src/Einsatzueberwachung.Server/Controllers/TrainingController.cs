using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Einsatzueberwachung.Server.Controllers;

[ApiController]
[EnableCors("TrainingApi")]
[Route("api/training")]
[Produces("application/json")]
public sealed class TrainingController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;
    private readonly IEinsatzService _einsatzService;
    private readonly ITrainingExerciseService _exerciseService;
    private readonly IOptionsMonitor<TrainingApiOptions> _options;
    private readonly ILogger<TrainingController> _logger;

    public TrainingController(
        IMasterDataService masterDataService,
        IEinsatzService einsatzService,
        ITrainingExerciseService exerciseService,
        IOptionsMonitor<TrainingApiOptions> options,
        ILogger<TrainingController> logger)
    {
        _masterDataService = masterDataService;
        _einsatzService = einsatzService;
        _exerciseService = exerciseService;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Liefert den technischen Status der Trainings-API inkl. Versionsinformationen.
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
        var config = _options.CurrentValue;

        return Ok(new TrainingHealthDto(
            "ok",
            config.ApiVersion,
            version,
            DateTime.UtcNow,
            config.InstanceName));
    }

    /// <summary>
    /// Liefert die aktuell aktivierten Trainings-Capabilities.
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult GetCapabilities()
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var writeAllowed = _options.CurrentValue.AllowWriteOperations;

        return Ok(new TrainingCapabilitiesDto(
            true,
            true,
            true,
            true,
            true,
            writeAllowed,
            writeAllowed,
            writeAllowed,
            writeAllowed,
            writeAllowed));
    }

    /// <summary>
    /// Gibt die verfuegbaren Personal-Stammdaten fuer Trainingszwecke zurueck.
    /// </summary>
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

    /// <summary>
    /// Gibt die verfuegbaren Hunde-Stammdaten fuer Trainingszwecke zurueck.
    /// </summary>
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

    /// <summary>
    /// Gibt die verfuegbaren Drohnen-Stammdaten fuer Trainingszwecke zurueck.
    /// </summary>
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

    /// <summary>
    /// Gibt eine aktuelle Team-Sicht fuer Trainingsabgleiche zurueck.
    /// </summary>
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

    /// <summary>
    /// Liefert einen konsistenten Ressourcen-Snapshot (Personal, Hunde, Drohnen, Teams).
    /// </summary>
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

    /// <summary>
    /// Legt einen neuen Trainingslauf an. Nur aktiv, wenn Schreiboperationen freigegeben sind.
    /// </summary>
    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercises(CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var list = await _exerciseService.GetExercisesAsync(cancellationToken);
        return Ok(new { snapshotUtc = DateTime.UtcNow, exercises = list, count = list.Count });
    }

    [HttpPost("exercises")]
    public async Task<IActionResult> CreateExercise([FromBody] CreateTrainingExerciseRequest request, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        if (!IsWriteAllowed())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Training write operations are disabled." });
        }

        if (!request.IsTraining)
        {
            return BadRequest(new { error = "Request must be marked as training (isTraining=true)." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        var result = await _exerciseService.CreateExerciseAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCapabilities), new { }, result);
    }

    /// <summary>
    /// Spiegelt ein Trainingsereignis in den Trainingslauf.
    /// </summary>
    [HttpPost("exercises/{exerciseId}/events")]
    public async Task<IActionResult> MirrorEvent(string exerciseId, [FromBody] MirrorTrainingEventRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.MirrorEventAsync(exerciseId, request, cancellationToken));
    }

    /// <summary>
    /// Spiegelt eine Trainingsentscheidung in den Trainingslauf.
    /// </summary>
    [HttpPost("exercises/{exerciseId}/decisions")]
    public async Task<IActionResult> MirrorDecision(string exerciseId, [FromBody] MirrorTrainingDecisionRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.MirrorDecisionAsync(exerciseId, request, cancellationToken));
    }

    /// <summary>
    /// Markiert einen Trainingslauf als abgeschlossen.
    /// </summary>
    [HttpPost("exercises/{exerciseId}/complete")]
    public async Task<IActionResult> CompleteExercise(string exerciseId, [FromBody] CompleteTrainingExerciseRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.CompleteExerciseAsync(exerciseId, request, cancellationToken));
    }

    /// <summary>
    /// Uebermittelt einen Trainingsbericht zu einem Trainingslauf.
    /// </summary>
    [HttpPost("exercises/{exerciseId}/report")]
    public async Task<IActionResult> SubmitReport(string exerciseId, [FromBody] SubmitTrainingReportRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.SubmitReportAsync(exerciseId, request, cancellationToken));
    }

    /// <summary>
    /// Fuegt einen trainerseitigen Eintrag (Feedback, Notiz, Funkspruch) zu einer Uebung hinzu.
    /// </summary>
    [HttpPost("exercises/{exerciseId}/trainer-entry")]
    public async Task<IActionResult> AddTrainerEntry(string exerciseId, [FromBody] AddTrainingTrainerEntryRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.AddTrainerEntryAsync(exerciseId, request, cancellationToken));
    }

    private bool IsEnabled() => _options.CurrentValue.Enabled;

    private bool IsWriteAllowed() => _options.CurrentValue.AllowWriteOperations;

    private async Task<IActionResult> ExecuteWriteOperationAsync(string exerciseId, bool isTraining, Func<Task<TrainingExerciseMirrorResultDto>> operation)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        if (!IsWriteAllowed())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Training write operations are disabled." });
        }

        if (!isTraining)
        {
            return BadRequest(new { error = "Request must be marked as training (isTraining=true)." });
        }

        if (string.IsNullOrWhiteSpace(exerciseId))
        {
            return BadRequest(new { error = "Exercise id is required." });
        }

        try
        {
            var result = await operation();
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training write operation failed for exercise {ExerciseId}", exerciseId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Training write operation failed." });
        }
    }

    private static string ResolveTeamStatus(Einsatzueberwachung.Domain.Models.Team team)
    {
        if (!team.IsRunning)
        {
            return "bereit";
        }

        if (team.IsSecondWarning)
        {
            return "kritisch";
        }

        if (team.IsFirstWarning)
        {
            return "warnung";
        }

        return "im_einsatz";
    }
}
