using System.Collections.Generic;

namespace Einsatzueberwachung.Domain.Models
{
    /// <summary>
    /// Describes a warning source that is registered in code.
    /// Used to populate the Warnzentrum configuration UI.
    /// </summary>
    public class WarningRuleDefinition
    {
        /// <summary>Source identifier — must match the <see cref="WarningEntry.Source"/> value used in code.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Short human-readable name shown in the UI.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Longer description explaining when this warning fires.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Default warning level as coded at the call-site.</summary>
        public WarningLevel DefaultLevel { get; init; } = WarningLevel.Warning;

        /// <summary>All warning sources that exist in the application.</summary>
        public static IReadOnlyList<WarningRuleDefinition> All { get; } =
        [
            new WarningRuleDefinition
            {
                Source = Sources.TeamTimer,
                Label = "Team-Timer: erste Warnstufe",
                Description = "Wird ausgelöst, wenn ein Team die erste Warnstufe (konfigurierbare Minuten) überschreitet.",
                DefaultLevel = WarningLevel.Warning
            },
            new WarningRuleDefinition
            {
                Source = Sources.TeamTimerCritical,
                Label = "Team-Timer: kritische Warnstufe",
                Description = "Wird ausgelöst, wenn ein Team die zweite/kritische Warnstufe überschreitet. Erfordert sofortige Aufmerksamkeit.",
                DefaultLevel = WarningLevel.Critical
            },
            new WarningRuleDefinition
            {
                Source = Sources.DogPause,
                Label = "Hund braucht Pause",
                Description = "Wird ausgelöst, wenn ein Hundeteam die konfigurierte Arbeitsdauer überschritten hat und eine Pflichtpause benötigt.",
                DefaultLevel = WarningLevel.Warning
            },
            new WarningRuleDefinition
            {
                Source = Sources.CollarOutOfBounds,
                Label = "Hund hat Suchgebiet verlassen",
                Description = "Wird ausgelöst, wenn ein GPS-Halsband das zugewiesene Suchgebiet des Teams verlässt.",
                DefaultLevel = WarningLevel.Critical
            }
        ];

        /// <summary>Canonical source identifier constants — use these instead of string literals.</summary>
        public static class Sources
        {
            public const string TeamTimer         = "TeamTimer";
            public const string TeamTimerCritical = "TeamTimerCritical";
            public const string DogPause          = "DogPause";
            public const string CollarOutOfBounds = "CollarOutOfBounds";
        }
    }
}
