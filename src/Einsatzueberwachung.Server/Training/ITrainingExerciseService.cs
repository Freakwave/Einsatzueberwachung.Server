namespace Einsatzueberwachung.Server.Training;

public interface ITrainingExerciseService
{
    Task<TrainingExerciseCreatedDto> CreateExerciseAsync(CreateTrainingExerciseRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> MirrorEventAsync(string exerciseId, MirrorTrainingEventRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> MirrorDecisionAsync(string exerciseId, MirrorTrainingDecisionRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> CompleteExerciseAsync(string exerciseId, CompleteTrainingExerciseRequest request, CancellationToken cancellationToken);
    Task<TrainingExerciseMirrorResultDto> SubmitReportAsync(string exerciseId, SubmitTrainingReportRequest request, CancellationToken cancellationToken);
}
