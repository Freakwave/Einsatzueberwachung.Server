using System.Text.Json;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Server.Training;

public sealed class TrainingExerciseService : ITrainingExerciseService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _storePath;
    private readonly string _startPresetPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TrainingExerciseService> _logger;
    private List<TrainingExerciseRecord>? _cache;
    private TrainingStartPreset? _startPreset;

    public TrainingExerciseService(ILogger<TrainingExerciseService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(AppPathResolver.GetDataDirectory(), "training-exercises.json");
        _startPresetPath = Path.Combine(AppPathResolver.GetDataDirectory(), "training-start-preset.json");
    }

    public async Task SetStartPresetAsync(TrainingStartPreset preset, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _startPreset = preset;
            var json = JsonSerializer.Serialize(_startPreset, JsonOptions);
            await File.WriteAllTextAsync(_startPresetPath, json, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrainingStartPreset?> GetStartPresetAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_startPreset is not null)
            {
                return _startPreset;
            }

            if (!File.Exists(_startPresetPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_startPresetPath, cancellationToken);
            _startPreset = JsonSerializer.Deserialize<TrainingStartPreset>(json);
            return _startPreset;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearStartPresetAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _startPreset = null;
            if (File.Exists(_startPresetPath))
            {
                File.Delete(_startPresetPath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

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

    private async Task<List<TrainingExerciseRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_storePath))
        {
            _cache = new List<TrainingExerciseRecord>();
            return _cache;
        }

        var json = await File.ReadAllTextAsync(_storePath, cancellationToken);
        _cache = JsonSerializer.Deserialize<List<TrainingExerciseRecord>>(json) ?? new List<TrainingExerciseRecord>();
        return _cache;
    }

    private async Task SaveAsync(List<TrainingExerciseRecord> records, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(_storePath, json, cancellationToken);
        _cache = records;
    }

    private static TrainingExerciseRecord RequireExercise(IEnumerable<TrainingExerciseRecord> list, string exerciseId)
    {
        var exercise = list.FirstOrDefault(x => string.Equals(x.Id, exerciseId, StringComparison.OrdinalIgnoreCase));
        if (exercise is null)
        {
            throw new KeyNotFoundException($"Training exercise '{exerciseId}' was not found.");
        }

        return exercise;
    }
}
