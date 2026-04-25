namespace Einsatzueberwachung.Server.Training;

public sealed record TrainingHealthDto(
    string Status,
    string ApiVersion,
    string ServerVersion,
    DateTime TimestampUtc,
    string InstanceName);

public sealed record TrainingCapabilitiesDto(
    bool ReadPersonnel,
    bool ReadDogs,
    bool ReadDrones,
    bool ReadTeams,
    bool ReadResourcesSnapshot,
    bool WriteExercises,
    bool WriteEvents,
    bool WriteDecisions,
    bool WriteComplete,
    bool WriteReport);

public sealed record TrainingResourceSnapshotDto(
    DateTime SnapshotUtc,
    int PersonnelCount,
    int DogCount,
    int DroneCount,
    int TeamCount,
    IReadOnlyList<TrainingPersonnelDto> Personnel,
    IReadOnlyList<TrainingDogDto> Dogs,
    IReadOnlyList<TrainingDroneDto> Drones,
    IReadOnlyList<TrainingTeamDto> Teams);

public sealed record TrainingPersonnelDto(
    string Id,
    string FullName,
    string Skills,
    bool IsActive,
    bool IsTrainingEligible);

public sealed record TrainingDogDto(
    string Id,
    string Name,
    string Breed,
    int Age,
    string Specializations,
    bool IsActive,
    bool IsTrainingEligible,
    IReadOnlyList<string> HandlerIds);

public sealed record TrainingDroneDto(
    string Id,
    string Name,
    string Model,
    string Manufacturer,
    string PilotId,
    bool IsActive,
    bool IsTrainingEligible);

public sealed record TrainingTeamDto(
    string TeamId,
    string TeamName,
    string DogId,
    string DogName,
    string HandlerId,
    string HandlerName,
    bool IsRunning,
    string Status,
    bool IsTrainingEligible);

public sealed record CreateTrainingExerciseRequest(
    string ExternalReference,
    string Name,
    string Scenario,
    string Location,
    DateTime? PlannedStartUtc,
    bool IsTraining,
    string Initiator);

public sealed record MirrorTrainingEventRequest(
    string Type,
    string Text,
    DateTime? OccurredAtUtc,
    bool IsTraining,
    string SourceSystem,
    string SourceUser);

public sealed record MirrorTrainingDecisionRequest(
    string Category,
    string Decision,
    string Rationale,
    DateTime? OccurredAtUtc,
    bool IsTraining,
    string SourceSystem,
    string SourceUser);

public sealed record CompleteTrainingExerciseRequest(
    string Summary,
    DateTime? CompletedAtUtc,
    bool IsTraining,
    string SourceSystem,
    string SourceUser);

public sealed record SubmitTrainingReportRequest(
    string Title,
    string Content,
    DateTime? ReportedAtUtc,
    bool IsTraining,
    string SourceSystem,
    string SourceUser);

public sealed record TrainingExerciseCreatedDto(
    string ExerciseId,
    string ExternalReference,
    string Name,
    string Scenario,
    string Location,
    DateTime CreatedAtUtc,
    string Status,
    bool IsTraining);

public sealed record TrainingExerciseMirrorResultDto(
    string ExerciseId,
    string Status,
    DateTime TimestampUtc,
    bool IsTraining);

public sealed class TrainingExerciseRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ExternalReference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PlannedStartUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "open";
    public bool IsTraining { get; set; } = true;
    public string Initiator { get; set; } = string.Empty;
    public List<TrainingMirrorEvent> Events { get; set; } = new();
    public List<TrainingMirrorDecision> Decisions { get; set; } = new();
    public List<TrainingReportItem> Reports { get; set; } = new();
}

public sealed class TrainingMirrorEvent
{
    public DateTime TimestampUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceUser { get; set; } = string.Empty;
}

public sealed class TrainingMirrorDecision
{
    public DateTime TimestampUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceUser { get; set; } = string.Empty;
}

public sealed class TrainingReportItem
{
    public DateTime TimestampUtc { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceUser { get; set; } = string.Empty;
}
