namespace Einsatzueberwachung.Domain.Models
{
    /// <summary>
    /// User-configurable behaviour for a single warning source.
    /// Persisted in AppSettings.WarningRules (keyed by source name).
    /// </summary>
    public class WarningRuleConfig
    {
        /// <summary>When false, warnings from this source are silently dropped.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// When set, overrides the level coded at the call-site.
        /// Null means "use the level that the code provides".
        /// </summary>
        public WarningLevel? LevelOverride { get; set; }
    }
}
