namespace Einsatzueberwachung.Server.Training;

public sealed partial class TrainingExerciseService
{
    public async Task AddScheduledEventAsync(string exerciseId, AddScheduledEventRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);
            exercise.ScheduledEvents.Add(new ScheduledTrainingEvent
            {
                DelayMinutes = request.DelayMinutes,
                Text = request.Text?.Trim() ?? string.Empty,
                EventType = request.EventType?.Trim().ToLowerInvariant() ?? "funk",
                TeamName = request.TeamName?.Trim() ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            });
            await SaveAsync(list, cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task RemoveScheduledEventAsync(string exerciseId, string eventId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);
            exercise.ScheduledEvents.RemoveAll(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
            await SaveAsync(list, cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task MarkScheduledEventFiredAsync(string exerciseId, string eventId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);
            var ev = exercise.ScheduledEvents.FirstOrDefault(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
            if (ev is not null)
            {
                ev.FiredAtUtc = DateTime.UtcNow;
            }
            await SaveAsync(list, cancellationToken);
        }
        finally { _lock.Release(); }
    }

    public async Task SetEscalationLevelAsync(string exerciseId, SetEscalationLevelRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = await LoadAsync(cancellationToken);
            var exercise = RequireExercise(list, exerciseId);
            exercise.EscalationLevel = Math.Clamp(request.Level, 0, 3);
            await SaveAsync(list, cancellationToken);
            _logger.LogInformation("Exercise {Id} escalation set to level {Level}", exerciseId, exercise.EscalationLevel);
        }
        finally { _lock.Release(); }
    }
}
