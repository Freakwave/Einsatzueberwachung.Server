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
        /// Minimum time in seconds before a warning from the same source/context may be repeated.
        /// A value of 0 disables throttling.
        /// </summary>
        public int CooldownSeconds { get; set; } = 30;

        /// <summary>
        /// When set, overrides the level coded at the call-site.
        /// Null means "use the level that the code provides".
        /// </summary>
        public WarningLevel? LevelOverride { get; set; }
    }
}
