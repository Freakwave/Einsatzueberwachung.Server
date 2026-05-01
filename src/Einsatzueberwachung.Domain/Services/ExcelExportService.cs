// Excel Import/Export Service für Stammdaten
// Verwendet ClosedXML für Excel-Operationen

using ClosedXML.Excel;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class ExcelExportService : IExcelExportService
    {
        private readonly IMasterDataService _masterDataService;

        public ExcelExportService(IMasterDataService masterDataService)
        {
            _masterDataService = masterDataService;
        }

        public async Task<byte[]> ExportStammdatenAsync()
        {
            using var workbook = new XLWorkbook();

            var personalList = await _masterDataService.GetPersonalListAsync();
            await ExportPersonalSheet(workbook, personalList);

            var dogList = await _masterDataService.GetDogListAsync();
            await ExportHundeSheet(workbook, dogList, personalList);

            var droneList = await _masterDataService.GetDroneListAsync();
            await ExportDrohnenSheet(workbook, droneList, personalList);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
