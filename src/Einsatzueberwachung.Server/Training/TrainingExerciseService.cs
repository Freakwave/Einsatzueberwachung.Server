using System.Text.Json;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Server.Training;

public sealed partial class TrainingExerciseService : ITrainingExerciseService
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
