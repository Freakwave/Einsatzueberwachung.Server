namespace Einsatzueberwachung.Server.Training;

public sealed partial class TrainingExerciseService
{
    public async Task<TrainingExerciseMirrorResultDto> MirrorEventAsync(string exerciseId, MirrorTrainingEventRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);

            exercise.Events.Add(new TrainingMirrorEvent
            {
                TimestampUtc = request.OccurredAtUtc ?? DateTime.UtcNow,
                Type = request.Type?.Trim() ?? "event",
                Text = request.Text?.Trim() ?? string.Empty,
                SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
                SourceUser = request.SourceUser?.Trim() ?? string.Empty
            });

            await SaveAsync(list, cancellationToken);
            return new TrainingExerciseMirrorResultDto(exercise.Id, exercise.Status, DateTime.UtcNow, true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseMirrorResultDto> MirrorDecisionAsync(string exerciseId, MirrorTrainingDecisionRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);

            exercise.Decisions.Add(new TrainingMirrorDecision
            {
                TimestampUtc = request.OccurredAtUtc ?? DateTime.UtcNow,
                Category = request.Category?.Trim() ?? string.Empty,
                Decision = request.Decision?.Trim() ?? string.Empty,
                Rationale = request.Rationale?.Trim() ?? string.Empty,
                SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
                SourceUser = request.SourceUser?.Trim() ?? string.Empty
            });

            await SaveAsync(list, cancellationToken);
            return new TrainingExerciseMirrorResultDto(exercise.Id, exercise.Status, DateTime.UtcNow, true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseMirrorResultDto> CompleteExerciseAsync(string exerciseId, CompleteTrainingExerciseRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);

            exercise.Events.Add(new TrainingMirrorEvent
            {
                TimestampUtc = request.CompletedAtUtc ?? DateTime.UtcNow,
                Type = "complete",
                Text = request.Summary?.Trim() ?? string.Empty,
                SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
                SourceUser = request.SourceUser?.Trim() ?? string.Empty
            });

            exercise.Status = "completed";
            exercise.CompletedAtUtc = request.CompletedAtUtc ?? DateTime.UtcNow;

            await SaveAsync(list, cancellationToken);
            return new TrainingExerciseMirrorResultDto(exercise.Id, exercise.Status, DateTime.UtcNow, true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseMirrorResultDto> SubmitReportAsync(string exerciseId, SubmitTrainingReportRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);

            exercise.Reports.Add(new TrainingReportItem
            {
                TimestampUtc = request.ReportedAtUtc ?? DateTime.UtcNow,
                Title = request.Title?.Trim() ?? string.Empty,
                Content = request.Content?.Trim() ?? string.Empty,
                SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
                SourceUser = request.SourceUser?.Trim() ?? string.Empty
            });

            await SaveAsync(list, cancellationToken);
            return new TrainingExerciseMirrorResultDto(exercise.Id, exercise.Status, DateTime.UtcNow, true);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingExerciseMirrorResultDto> AddTrainerEntryAsync(string exerciseId, AddTrainingTrainerEntryRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);

            exercise.TrainerEntries.Add(new TrainingTrainerEntry
            {
                TimestampUtc = request.OccurredAtUtc ?? DateTime.UtcNow,
                EntryType = request.EntryType?.Trim().ToLowerInvariant() ?? "note",
                Text = request.Text?.Trim() ?? string.Empty,
                ParticipantId = request.ParticipantId?.Trim() ?? string.Empty,
                ParticipantName = request.ParticipantName?.Trim() ?? string.Empty,
                SourceSystem = request.SourceSystem?.Trim() ?? "TrainerPortal",
                SourceUser = request.SourceUser?.Trim() ?? "Trainer"
            });

            await SaveAsync(list, cancellationToken);
            return new TrainingExerciseMirrorResultDto(exercise.Id, exercise.Status, DateTime.UtcNow, true);
        }
        finally
        {
            _lock.Release();
        }
    }
}
