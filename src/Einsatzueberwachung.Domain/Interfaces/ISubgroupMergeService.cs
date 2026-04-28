// Service-Interface für die Teilgruppen-Zusammenführung

using System.Collections.Generic;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Interfaces
{
    /// <summary>
    /// Verwaltet den vollständigen Merge-Workflow für Teilgruppen-Exporte.
    /// </summary>
    public interface ISubgroupMergeService
    {
        /// <summary>
        /// Erstellt eine neue Merge-Session aus einem importierten Export-Paket.
        /// Analysiert Übereinstimmungen und befüllt alle Vorschlagslisten.
        /// Schreibt noch NICHTS — nur Analyse.
        /// </summary>
        /// <param name="packet">Das importierte Export-Paket.</param>
        /// <param name="targetArchivedEinsatzId">null = aktiver Einsatz, sonst ID des archivierten Einsatzes.</param>
        Task<SubgroupMergeSession> CreateSessionAsync(
            SubgroupExportPacket packet,
            string? targetArchivedEinsatzId = null);

        /// <summary>
        /// Aktualisiert das ID-Remapping und die aufgelösten Team-Member-Namen
        /// nachdem der Benutzer eine Stammdaten-Entscheidung getroffen hat.
        /// Muss nach jeder Änderung an einer MasterDataMergeItem-Entscheidung aufgerufen werden.
        /// </summary>
        void RebuildIdRemapping(SubgroupMergeSession session);

        /// <summary>
        /// Führt eine vollständig vorbereitete Session atomar aus.
        /// Alle Stammdaten-Items müssen entschieden sein.
        /// </summary>
        /// <returns>Der Protokolleintrag für die abgeschlossene Zusammenführung.</returns>
        Task<MergeHistoryEntry> ApplyMergeAsync(SubgroupMergeSession session);

        /// <summary>
        /// Macht eine Zusammenführung vollständig rückgängig anhand des Protokolleintrags.
        /// Entfernt alle hinzugefügten Teams, Notizen, Gebiete, Marker und neu erstellte Stammdaten.
        /// </summary>
        /// <param name="mergeId">ID der rückgängig zu machenden Zusammenführung.</param>
        /// <param name="archivedEinsatzId">null = aktiver Einsatz, sonst ID des archivierten Einsatzes.</param>
        Task RevertMergeAsync(string mergeId, string? archivedEinsatzId = null);

        /// <summary>
        /// Gibt die Merge-Historie für den aktiven oder einen archivierten Einsatz zurück.
        /// </summary>
        Task<List<MergeHistoryEntry>> GetMergeHistoryAsync(string? archivedEinsatzId = null);

        /// <summary>
        /// Liest und deserialisiert ein Upload-Paket aus dem JSON-Byte-Array.
        /// </summary>
        SubgroupExportPacket? ParseExportPacket(byte[] json);
    }
}
