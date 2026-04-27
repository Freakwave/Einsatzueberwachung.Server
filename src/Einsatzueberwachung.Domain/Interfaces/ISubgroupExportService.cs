// Service-Interface für den Teilgruppen-Export

using System.Collections.Generic;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Interfaces
{
    /// <summary>
    /// Erstellt ein portables Export-Paket aus einem laufenden Einsatz,
    /// das von einer Teilgruppe (Staffel / Untereinheit) mitgenommen und
    /// später über <see cref="ISubgroupMergeService"/> wieder integriert werden kann.
    /// </summary>
    public interface ISubgroupExportService
    {
        /// <summary>
        /// Erstellt ein Export-Paket für die angegebenen Teams.
        /// Stammdaten werden automatisch aus den Team-Referenzen gesammelt.
        /// </summary>
        /// <param name="selectedTeamIds">IDs der Teams, die zur Teilgruppe gehören.</param>
        /// <param name="subgroupName">Name der exportierenden Teilgruppe (z.B. "Staffel Nord").</param>
        /// <param name="options">Optionale Export-Einstellungen.</param>
        Task<SubgroupExportPacket> BuildExportPacketAsync(
            IEnumerable<string> selectedTeamIds,
            string subgroupName,
            SubgroupExportOptions? options = null);

        /// <summary>
        /// Serialisiert ein Export-Paket als UTF-8 JSON-Byte-Array (direkt für den Download).
        /// </summary>
        byte[] Serialize(SubgroupExportPacket packet);

        /// <summary>
        /// Erstellt den vorgeschlagenen Dateinamen für ein Export-Paket.
        /// Format: <c>&lt;SubgroupName&gt;_&lt;EinsatzNr&gt;_&lt;YYYYMMDD_HHmm&gt;.einsatz-export.json</c>
        /// </summary>
        string GetFileName(SubgroupExportPacket packet);
    }

    /// <summary>
    /// Konfiguriert, welche Daten in ein Export-Paket aufgenommen werden.
    /// </summary>
    public class SubgroupExportOptions
    {
        /// <summary>Alle globalen Notizen (nicht nur team-spezifische) einschließen. Standard: false.</summary>
        public bool IncludeGlobalNotes { get; set; } = false;

        /// <summary>Nur Notizen der ausgewählten Teams einschließen. Standard: true.</summary>
        public bool IncludeTeamNotes { get; set; } = true;

        /// <summary>System-generierte Notizen einschließen. Standard: false.</summary>
        public bool IncludeSystemNotes { get; set; } = false;

        /// <summary>Suchgebiete einschließen, die den ausgewählten Teams zugewiesen sind. Standard: true.</summary>
        public bool IncludeAssignedSearchAreas { get; set; } = true;

        /// <summary>Alle Suchgebiete des Einsatzes einschließen (unabhängig von Team-Zuordnung). Standard: false.</summary>
        public bool IncludeAllSearchAreas { get; set; } = false;

        /// <summary>Karten-Marker einschließen. Standard: true.</summary>
        public bool IncludeMapMarkers { get; set; } = true;

        /// <summary>GPS-Tracks der ausgewählten Teams einschließen. Standard: true.</summary>
        public bool IncludeTrackSnapshots { get; set; } = true;
    }
}
