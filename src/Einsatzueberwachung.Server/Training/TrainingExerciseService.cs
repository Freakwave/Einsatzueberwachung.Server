using System.Text.Json;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Server.Training;

public sealed class TrainingExerciseService : ITrainingExerciseService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _storePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TrainingExerciseService> _logger;
    private List<TrainingExerciseRecord>? _cache;

    public TrainingExerciseService(ILogger<TrainingExerciseService> logger)
    {
        _logger = logger;
        _storePath = Path.Combine(AppPathResolver.GetDataDirectory(), "training-exercises.json");
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
                Status = "open"
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
