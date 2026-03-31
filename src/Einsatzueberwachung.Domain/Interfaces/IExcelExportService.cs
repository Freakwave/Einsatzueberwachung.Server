// Service-Interface für Excel Import/Export von Stammdaten

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public interface IExcelExportService
    {
        /// <summary>
        /// Exportiert alle Stammdaten in eine Excel-Datei
        /// </summary>
        Task<byte[]> ExportStammdatenAsync();

        /// <summary>
        /// Importiert Stammdaten aus einer Excel-Datei
        /// </summary>
        /// <param name="excelData">Die Excel-Datei als Byte-Array</param>
        /// <returns>Import-Ergebnis mit Statistiken</returns>
        Task<ImportResult> ImportStammdatenAsync(byte[] excelData);

        /// <summary>
        /// Erstellt eine leere Excel-Vorlage für den Import
        /// </summary>
        Task<byte[]> CreateImportTemplateAsync();
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int PersonalImported { get; set; }
        public int HundeImported { get; set; }
        public int DrohnenImported { get; set; }
        public int PersonalSkipped { get; set; }
        public int HundeSkipped { get; set; }
        public int DrohnenSkipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public int TotalImported => PersonalImported + HundeImported + DrohnenImported;
        public int TotalSkipped => PersonalSkipped + HundeSkipped + DrohnenSkipped;
    }
}
