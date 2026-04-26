using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Server.Training;

public sealed class TrainingScenarioSuggestionService : ITrainingScenarioSuggestionService
{
    private static readonly List<TrainingScenarioSuggestionItem> Catalog =
    [
        new("Vermisstensuche Wald", "Sucheinsatz", "Eine orientierungslose Person wurde zuletzt am Waldrand gesehen.", "Personensuche", ["Flachenlage", "Nacht", "Rettungskette"]),
        new("Mantrailing Innenstadt", "Mantrailing", "Startpunkt am Parkplatz, Spur in Richtung Innenstadt.", "Spurarbeit", ["Verkehr", "Ablenkung", "Zeugenbefragung"]),
        new("Flussufer-Abschnitt", "Vermisstensuche", "Rucksackfund am Ufer, Strömung und Unterholz erschweren die Suche.", "Wassernahe Suche", ["Abschnittssuche", "Sicherheit", "Koordination"]),
        new("Drohnenunterstuetzte Flaechensuche", "Sucheinsatz", "Grosses Feldgebiet mit schlechter Sicht. Drohnen und Hundeteams im Verbund.", "Luft-Boden-Koordination", ["Drohne", "Lagekarte", "Sektorplanung"]),
        new("Truemmersuche Uebung", "Rettungshunde", "Person vermisst nach Teileinsturz in Industriehalle.", "Technische Suche", ["Sicherung", "Absperrung", "Rettungspfade"])
    ];

    private static readonly IReadOnlyList<string> PromptExamples =
    [
        "Erstelle eine realistische Suchhundelagen-Uebung fuer [Ort] mit 3 Eskalationspunkten, Rollenverteilung, Sicherheitsregeln und klaren Lernzielen.",
        "Simuliere eine Mantrailing-Uebung mit Startlage, 5 Funkmeldungen, moeglichen Fehlentscheidungen und Debriefing-Fragen fuer den Trainer.",
        "Generiere eine Vermisstensuche fuer ein gemischtes Team (Hund, Drohne, ELW) inklusive Zeitplan, Lageupdates und Bewertungskriterien."
    ];

    public TrainingScenarioSuggestionResult BuildSuggestions(bool isExerciseMode, string? location, IReadOnlyList<Team> teams, string? hint)
    {
        if (!isExerciseMode)
        {
            return new TrainingScenarioSuggestionResult([], PromptExamples, "KI-Vorschlaege sind aktiv, sobald ein Uebungsmodus laeuft.");
        }

        var normalizedHint = (hint ?? string.Empty).Trim().ToLowerInvariant();
        var hasDrone = teams.Any(t => t.IsDroneTeam);
        var hasRunningTeams = teams.Any(t => t.IsRunning);

        var ranked = Catalog
            .Select(item => new { item, score = Score(item, normalizedHint, hasDrone, hasRunningTeams, location) })
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.Title)
            .Take(3)
            .Select(x => x.item)
            .ToList();

        if (ranked.Count == 0)
        {
            ranked = Catalog.Take(3).ToList();
        }

        return new TrainingScenarioSuggestionResult(
            ranked,
            PromptExamples,
            "Vorschlaege basieren auf Lagekontext, aktiven Ressourcen und optionalem Hinweistext.");
    }

    private static int Score(TrainingScenarioSuggestionItem item, string hint, bool hasDrone, bool hasRunningTeams, string? location)
    {
        var score = 0;
        var haystack = $"{item.Title} {item.Type} {item.Description} {string.Join(' ', item.Tags)}".ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(hint) && haystack.Contains(hint, StringComparison.Ordinal))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(location) && item.Description.Contains("Ort", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        if (hasDrone && item.Tags.Any(t => t.Contains("drohne", StringComparison.OrdinalIgnoreCase)))
        {
            score += 3;
        }

        if (hasRunningTeams && item.Tags.Any(t => t.Contains("koordination", StringComparison.OrdinalIgnoreCase)))
        {
            score += 2;
        }

        return score;
    }
}

public sealed record TrainingScenarioSuggestionResult(
    IReadOnlyList<TrainingScenarioSuggestionItem> Suggestions,
    IReadOnlyList<string> PromptExamples,
    string InfoText);

public sealed record TrainingScenarioSuggestionItem(
    string Title,
    string Type,
    string Description,
    string Focus,
    IReadOnlyList<string> Tags);
