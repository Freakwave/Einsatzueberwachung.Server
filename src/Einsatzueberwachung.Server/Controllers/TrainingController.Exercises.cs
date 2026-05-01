using Einsatzueberwachung.Server.Training;
using Microsoft.AspNetCore.Mvc;

namespace Einsatzueberwachung.Server.Controllers;

public sealed partial class TrainingController
{
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

    [HttpPost("exercises/{exerciseId}/events")]
    public async Task<IActionResult> MirrorEvent(string exerciseId, [FromBody] MirrorTrainingEventRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.MirrorEventAsync(exerciseId, request, cancellationToken));
    }

    [HttpPost("exercises/{exerciseId}/decisions")]
    public async Task<IActionResult> MirrorDecision(string exerciseId, [FromBody] MirrorTrainingDecisionRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.MirrorDecisionAsync(exerciseId, request, cancellationToken));
    }

    [HttpPost("exercises/{exerciseId}/complete")]
    public async Task<IActionResult> CompleteExercise(string exerciseId, [FromBody] CompleteTrainingExerciseRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.CompleteExerciseAsync(exerciseId, request, cancellationToken));
    }

    [HttpPost("exercises/{exerciseId}/report")]
    public async Task<IActionResult> SubmitReport(string exerciseId, [FromBody] SubmitTrainingReportRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.SubmitReportAsync(exerciseId, request, cancellationToken));
    }

    [HttpPost("exercises/{exerciseId}/trainer-entry")]
    public async Task<IActionResult> AddTrainerEntry(string exerciseId, [FromBody] AddTrainingTrainerEntryRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWriteOperationAsync(
            exerciseId,
            request.IsTraining,
            () => _exerciseService.AddTrainerEntryAsync(exerciseId, request, cancellationToken));
    }
}
