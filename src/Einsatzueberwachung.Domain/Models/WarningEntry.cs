using System;

namespace Einsatzueberwachung.Domain.Models
{
    public class WarningEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public WarningLevel Level { get; set; } = WarningLevel.Warning;
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional URL to navigate to when the user clicks the warning toast.
        /// </summary>
        public string? NavigationUrl { get; set; }

        /// <summary>
        /// Optional team ID associated with this warning.
        /// </summary>
        public string? TeamId { get; set; }

        /// <summary>
        /// Human-readable source description (e.g. "TeamTimer", "GPS", "Akku").
        /// </summary>
        public string? Source { get; set; }
    }

    public enum WarningLevel
    {
        Info,
        Warning,
        Critical
    }
}
