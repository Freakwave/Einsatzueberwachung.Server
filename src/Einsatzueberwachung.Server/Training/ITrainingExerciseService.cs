namespace Einsatzueberwachung.Server.Training;

public interface ITrainingExerciseService
{
    Task<IReadOnlyList<TrainingExerciseRecord>> GetExercisesAsync(CancellationToken cancellationToken);
    Task<TrainingExerciseRecord?> GetExerciseAsync(string exerciseId, CancellationToken cancellationToken);
    Task SetStartPresetAsync(TrainingStartPreset preset, CancellationToken cancellationToken);
    Task<TrainingStartPreset?> GetStartPresetAsync(CancellationToken cancellationToken);
    Task ClearStartPresetAsync(CancellationToken cancellationToken);
    Task<TrainingExerciseCreatedDto> CreateExerciseAsync(CreateTrainingExerciseRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> MirrorEventAsync(string exerciseId, MirrorTrainingEventRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> MirrorDecisionAsync(string exerciseId, MirrorTrainingDecisionRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> CompleteExerciseAsync(string exerciseId, CompleteTrainingExerciseRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> SubmitReportAsync(string exerciseId, SubmitTrainingReportRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> AddTrainerEntryAsync(string exerciseId, AddTrainingTrainerEntryRequest request, CancellationToken cancellationToken);
    Task AddScheduledEventAsync(string exerciseId, AddScheduledEventRequest request, CancellationToken cancellationToken);
    Task RemoveScheduledEventAsync(string exerciseId, string eventId, CancellationToken cancellationToken);
    Task MarkScheduledEventFiredAsync(string exerciseId, string eventId, CancellationToken cancellationToken);
    Task SetEscalationLevelAsync(string exerciseId, SetEscalationLevelRequest request, CancellationToken cancellationToken);
}
