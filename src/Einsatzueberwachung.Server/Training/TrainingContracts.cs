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
    string Initiator,
    int? PlannedDurationMinutes = null);

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

public sealed record AddTrainingTrainerEntryRequest(
    string EntryType,
    string Text,
    string ParticipantId,
    string ParticipantName,
    DateTime? OccurredAtUtc,
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
    /// <summary>Optionale geplante Uebungsdauer in Minuten fuer Countdown-Anzeige.</summary>
    public int? PlannedDurationMinutes { get; set; }
    /// <summary>Aktuelle Eskalationsstufe: 0 = keine, 1-3 = eskaliert.</summary>
    public int EscalationLevel { get; set; } = 0;
    public List<TrainingMirrorEvent> Events { get; set; } = new();
    public List<TrainingMirrorDecision> Decisions { get; set; } = new();
    public List<TrainingReportItem> Reports { get; set; } = new();
    public List<TrainingTrainerEntry> TrainerEntries { get; set; } = new();
    public List<ScheduledTrainingEvent> ScheduledEvents { get; set; } = new();
}

public sealed class TrainingStartPreset
{
    public string ExerciseId { get; set; } = string.Empty;
    public string ExerciseName { get; set; } = string.Empty;
    public string ScenarioCategory { get; set; } = string.Empty;
    public string BriefingText { get; set; } = string.Empty;
    public string SuggestedLocation { get; set; } = string.Empty;
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
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

public sealed class TrainingTrainerEntry
{
    public DateTime TimestampUtc { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceUser { get; set; } = string.Empty;
}

/// <summary>Zeitgesteuertes Ereignis, das automatisch nach einer definierten Verzoegerung ausgeloest wird.</summary>
public sealed class ScheduledTrainingEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int DelayMinutes { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>"lage" oder "funk"</summary>
    public string EventType { get; set; } = "funk";
    public string TeamName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FiredAtUtc { get; set; }
    public bool IsFired => FiredAtUtc.HasValue;
}

public sealed record AddScheduledEventRequest(
    int DelayMinutes,
    string Text,
    string EventType,
    string TeamName);

public sealed record SetEscalationLevelRequest(
    int Level,
    string SourceUser);
