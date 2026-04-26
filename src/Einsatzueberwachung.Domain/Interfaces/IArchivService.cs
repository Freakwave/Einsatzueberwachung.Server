// Service-Interface fuer Einsatz-Archiv Verwaltung

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public interface IArchivService
    {
        /// <summary>
        /// Archiviert einen abgeschlossenen Einsatz
        /// </summary>
        Task<ArchivedEinsatz> ArchiveEinsatzAsync(
            EinsatzData einsatzData,
            string ergebnis,
            string bemerkungen,
            List<string>? personalVorOrt = null,
            List<string>? hundeVorOrt = null);

        /// <summary>
        /// Laedt alle archivierten Einsaetze
        /// </summary>
        Task<List<ArchivedEinsatz>> GetAllArchivedAsync();

        /// <summary>
        /// Laedt einen archivierten Einsatz nach ID
        /// </summary>
        Task<ArchivedEinsatz?> GetByIdAsync(string id);

        /// <summary>
        /// Sucht archivierte Einsaetze nach Kriterien
        /// </summary>
        Task<List<ArchivedEinsatz>> SearchAsync(ArchivSearchCriteria criteria);

        /// <summary>
        /// Loescht einen archivierten Einsatz
        /// </summary>
        Task<bool> DeleteAsync(string id);

        /// <summary>
        /// Exportiert alle archivierten Einsaetze als JSON
        /// </summary>
        Task<byte[]> ExportAllAsJsonAsync();

        /// <summary>
        /// Importiert archivierte Einsaetze aus JSON
        /// </summary>
        Task<int> ImportFromJsonAsync(byte[] jsonData);

        /// <summary>
        /// Gibt Statistiken ueber alle archivierten Einsaetze zurueck
        /// </summary>
        Task<ArchivStatistics> GetStatisticsAsync();
    }
}
