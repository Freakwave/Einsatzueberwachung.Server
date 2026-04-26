using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Server.Training;

public interface ITrainingScenarioSuggestionService
{
    TrainingScenarioSuggestionResult BuildSuggestions(bool isExerciseMode, string? location, IReadOnlyList<Team> teams, string? hint);
}
