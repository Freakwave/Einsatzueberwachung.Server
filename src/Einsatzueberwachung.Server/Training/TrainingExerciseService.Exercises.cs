namespace Einsatzueberwachung.Server.Training;

public sealed partial class TrainingExerciseService
{
    public async Task<IReadOnlyList<TrainingExerciseRecord>> GetExercisesAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            return list
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseRecord?> GetExerciseAsync(string exerciseId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            return list.FirstOrDefault(x => string.Equals(x.Id, exerciseId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseCreatedDto> CreateExerciseAsync(CreateTrainingExerciseRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);

            var exercise = new TrainingExerciseRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                ExternalReference = request.ExternalReference?.Trim() ?? string.Empty,
                Name = request.Name.Trim(),
                Scenario = request.Scenario?.Trim() ?? string.Empty,
                Location = request.Location?.Trim() ?? string.Empty,
                PlannedStartUtc = request.PlannedStartUtc,
                CreatedAtUtc = DateTime.UtcNow,
                IsTraining = true,
                Initiator = request.Initiator?.Trim() ?? string.Empty,
                Status = "open",
                PlannedDurationMinutes = request.PlannedDurationMinutes > 0 ? request.PlannedDurationMinutes : null
            };

            list.Add(exercise);
            await SaveAsync(list, cancellationToken);

            _logger.LogInformation("Training exercise created: {ExerciseId} ({Name})", exercise.Id, exercise.Name);

            return new TrainingExerciseCreatedDto(
                exercise.Id,
                exercise.ExternalReference,
                exercise.Name,
                exercise.Scenario,
                exercise.Location,
                exercise.CreatedAtUtc,
                exercise.Status,
                exercise.IsTraining);
        }
        finally
        {
            _lock.Release();
        }
    }
}
