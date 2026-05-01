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
public sealed partial class TrainingController : ControllerBase
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
