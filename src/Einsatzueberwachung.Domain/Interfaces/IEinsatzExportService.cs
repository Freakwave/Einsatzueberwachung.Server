// Service-Interface für den Einsatz-Export

using System.Collections.Generic;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Interfaces
{
    /// <summary>
    /// Erstellt ein portables Export-Paket aus einem laufenden Einsatz,
    /// das später über <see cref="IEinsatzMergeService"/> wieder integriert werden kann.
    /// </summary>
    public interface IEinsatzExportService
    {
        /// <summary>
        /// Erstellt ein Export-Paket für die angegebenen Teams.
        /// Stammdaten werden automatisch aus den Team-Referenzen gesammelt.
        /// </summary>
        /// <param name="selectedTeamIds">IDs der zu exportierenden Teams.</param>
        /// <param name="subgroupName">Optionaler Label-Name für das Paket (z. B. "Staffel Nord"). Kann leer sein.</param>
        /// <param name="options">Optionale Export-Einstellungen.</param>
        Task<EinsatzExportPacket> BuildExportPacketAsync(
            IEnumerable<string> selectedTeamIds,
            string subgroupName,
            EinsatzExportOptions? options = null);

        /// <summary>
        /// Serialisiert ein Export-Paket als UTF-8 JSON-Byte-Array (direkt für den Download).
        /// </summary>
        byte[] Serialize(EinsatzExportPacket packet);

        /// <summary>
        /// Erstellt den vorgeschlagenen Dateinamen für ein Export-Paket.
        /// Format: <c>&lt;EinsatzNr&gt;_&lt;YYYYMMDD_HHmm&gt;.einsatz-export.json</c>
        /// (oder <c>&lt;Label&gt;_&lt;EinsatzNr&gt;_…</c> wenn ein SubgroupName gesetzt ist).
        /// </summary>
        string GetFileName(EinsatzExportPacket packet);
    }

    /// <summary>
    /// Konfiguriert, welche Daten in ein Export-Paket aufgenommen werden.
    /// </summary>
    public class EinsatzExportOptions
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
