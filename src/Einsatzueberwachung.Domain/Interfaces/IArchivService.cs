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
        Task<ArchivedEinsatz> ArchiveEinsatzAsync(EinsatzData einsatzData, string ergebnis, string bemerkungen);

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

    /// <summary>
    /// Suchkriterien fuer das Archiv
    /// </summary>
    public class ArchivSearchCriteria
    {
        public string? Suchtext { get; set; }
        public DateTime? VonDatum { get; set; }
        public DateTime? BisDatum { get; set; }
        public bool? NurEinsaetze { get; set; } // true = nur Einsaetze, false = nur Uebungen, null = alle
        public string? Ergebnis { get; set; }
        public string? Einsatzort { get; set; }
    }

    /// <summary>
    /// Statistiken ueber das Archiv
    /// </summary>
    public class ArchivStatistics
    {
        public int GesamtAnzahl { get; set; }
        public int AnzahlEinsaetze { get; set; }
        public int AnzahlUebungen { get; set; }
        public int AnzahlDiesesJahr { get; set; }
        public int AnzahlDiesenMonat { get; set; }
        public TimeSpan DurchschnittlicheDauer { get; set; }
        public string HaeufigsterErfolgTyp { get; set; } = string.Empty;
        public int GesamtPersonalEinsaetze { get; set; }
        public int GesamtHundeEinsaetze { get; set; }
        public Dictionary<string, int> EinsaetzeProMonat { get; set; } = new();
        public Dictionary<string, int> EinsaetzeProJahr { get; set; } = new();
    }
}
