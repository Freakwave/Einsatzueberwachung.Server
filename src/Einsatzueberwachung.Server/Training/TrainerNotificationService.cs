namespace Einsatzueberwachung.Server.Training;

/// <summary>
/// Singleton-Dienst, der Trainer-Benachrichtigungen an alle verbundenen Blazor-Clients verteilt.
/// </summary>
public sealed class TrainerNotificationService
{
    /// <summary>
    /// Wird ausgeloest, wenn der Trainer eine Uebung beendet.
    /// Parameter: (exerciseName, summary)
    /// </summary>
    public event Action<string, string>? ExerciseEnded;

    public void FireExerciseEnded(string exerciseName, string summary)
    {
        ExerciseEnded?.Invoke(exerciseName, summary);
    }
}
