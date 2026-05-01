using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Services;
using Microsoft.AspNetCore.StaticFiles;
using System.IO.Compression;
using System.Text.Json;

namespace Einsatzueberwachung.Server.Extensions;

internal static class DownloadEndpoints
{
    public static WebApplication MapDownloadEndpoints(this WebApplication app)
    {
        app.MapGet("/downloads/einsatz-bericht.pdf", async (IEinsatzService einsatzService, IPdfExportService pdfExportService) =>
        {
            var einsatz = einsatzService.CurrentEinsatz;
            var fileNamePart = string.IsNullOrWhiteSpace(einsatz.EinsatzNummer)
                ? $"einsatzbericht-{DateTime.Now:yyyyMMdd-HHmmss}"
                : $"einsatzbericht-{einsatz.EinsatzNummer}";

            var bytes = await pdfExportService.ExportEinsatzToPdfBytesAsync(
                einsatz,
                einsatzService.Teams,
                einsatzService.GlobalNotes);

            return Results.File(bytes, "application/pdf", $"{fileNamePart}.pdf");
        });

        app.MapGet("/downloads/einsatz-karte.pdf", async (
            IEinsatzService einsatzService,
            IPdfExportService pdfExportService,
            string? mapType = null,
            string? teamId = null) =>
        {
            var tileType = mapType switch
            {
                "satellite" => Einsatzueberwachung.Domain.Models.Enums.MapTileType.Satellite,
                "topo" => Einsatzueberwachung.Domain.Models.Enums.MapTileType.Topographic,
                _ => Einsatzueberwachung.Domain.Models.Enums.MapTileType.Streets
            };

            var einsatz = einsatzService.CurrentEinsatz;
            var bytes = await pdfExportService.ExportEinsatzKarteToPdfBytesAsync(
                einsatz, einsatzService.Teams, tileType, teamId);

            var fileNamePart = string.IsNullOrWhiteSpace(einsatz.EinsatzNummer)
                ? $"einsatz-karte-{DateTime.Now:yyyyMMdd-HHmmss}"
                : $"einsatz-karte-{einsatz.EinsatzNummer}";
            return Results.File(bytes, "application/pdf", $"{fileNamePart}.pdf");
        });

        app.MapGet("/downloads/einsatz-archiv/{id}.pdf", async (string id, IArchivService archivService, IPdfExportService pdfExportService) =>
        {
            var archivedEinsatz = await archivService.GetByIdAsync(id);
            if (archivedEinsatz is null)
            {
                return Results.NotFound();
            }

            var includeTracks = archivedEinsatz.TrackSnapshots?.Any(track => track.Points.Count >= 2) == true;
            var bytes = await pdfExportService.ExportArchivedEinsatzToPdfBytesAsync(archivedEinsatz, includeTracks);
            var fileNamePart = string.IsNullOrWhiteSpace(archivedEinsatz.EinsatzNummer)
                ? $"einsatz-archiv-{archivedEinsatz.EinsatzDatum:yyyyMMdd-HHmmss}"
                : $"einsatz-archiv-{archivedEinsatz.EinsatzNummer}";

            return Results.File(bytes, "application/pdf", $"{fileNamePart}.pdf");
        });

        app.MapGet("/downloads/einsatz-archiv.json", async (IArchivService archivService) =>
        {
            var bytes = await archivService.ExportAllAsJsonAsync();
            return Results.File(bytes, "application/json", $"einsatz-archiv-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        });

        app.MapGet("/downloads/einsatz-bericht.xlsx", async (IEinsatzService einsatzService, IExcelExportService excelExportService) =>
        {
            var einsatz = einsatzService.CurrentEinsatz;
            var teams = einsatzService.Teams;
            var notes = einsatzService.GlobalNotes;
            var fileNamePart = string.IsNullOrWhiteSpace(einsatz.EinsatzNummer)
                ? $"einsatzbericht-{DateTime.Now:yyyyMMdd-HHmmss}"
                : einsatz.EinsatzNummer.Replace("/", "-").Replace(" ", "_");
            var bytes = await excelExportService.ExportEinsatzAsync(einsatz, teams, notes);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileNamePart}.xlsx");
        });

        app.MapGet("/downloads/app-settings.json", async (ISettingsService settingsService) =>
        {
            var settings = await settingsService.GetAppSettingsAsync();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true });
            return Results.File(bytes, "application/json", "app-settings.json");
        });

        app.MapGet("/downloads/staffel-settings.json", async (ISettingsService settingsService) =>
        {
            var settings = await settingsService.GetStaffelSettingsAsync();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true });
            return Results.File(bytes, "application/json", "staffel-settings.json");
        });

        app.MapGet("/downloads/staffel-logo", async (ISettingsService settingsService) =>
        {
            var settings = await settingsService.GetStaffelSettingsAsync();
            if (string.IsNullOrWhiteSpace(settings.StaffelLogoPfad))
            {
                return Results.NotFound();
            }

            var logoPath = settings.StaffelLogoPfad;
            if (!Path.IsPathRooted(logoPath))
            {
                logoPath = Path.Combine(AppPathResolver.GetDataDirectory(), logoPath.TrimStart('/', '\\'));
            }

            if (!File.Exists(logoPath))
            {
                return Results.NotFound();
            }

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            if (!contentTypeProvider.TryGetContentType(logoPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return Results.File(await File.ReadAllBytesAsync(logoPath), contentType);
        });

        app.MapGet("/downloads/session-data.json", async (IMasterDataService masterDataService) =>
        {
            var sessionData = await masterDataService.LoadSessionDataAsync();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(sessionData, new JsonSerializerOptions { WriteIndented = true });
            return Results.File(bytes, "application/json", "session-data.json");
        });

        app.MapGet("/downloads/stammdaten.xlsx", async (IExcelExportService excelExportService) =>
        {
            var bytes = await excelExportService.ExportStammdatenAsync();
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "stammdaten.xlsx");
        });

        app.MapGet("/downloads/stammdaten-template.xlsx", async (IExcelExportService excelExportService) =>
        {
            var bytes = await excelExportService.CreateImportTemplateAsync();
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "stammdaten-template.xlsx");
        });

        app.MapGet("/downloads/data-backup.zip", () =>
        {
            var dataDirectory = AppPathResolver.GetDataDirectory();
            if (!Directory.Exists(dataDirectory))
            {
                return Results.NotFound();
            }

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var files = Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories);
                foreach (var filePath in files)
                {
                    var relativePath = Path.GetRelativePath(dataDirectory, filePath);
                    archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
                }
            }

            var fileName = $"einsatzueberwachung-data-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            return Results.File(memoryStream.ToArray(), "application/zip", fileName);
        });

        app.MapGet("/downloads/livetracking.zip", () =>
        {
            var candidateDirectories = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "livetracking"),
                Path.Combine(AppPathResolver.GetDataDirectory(), "livetracking"),
                Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "Einsatzueberwachung.LiveTracking", "bin", "Release", "net9.0-windows7.0")),
                Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "Einsatzueberwachung.LiveTracking", "bin", "Debug", "net9.0-windows7.0"))
            };

            var sourceDirectory = candidateDirectories
                .Where(Directory.Exists)
                .FirstOrDefault(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any());

            if (sourceDirectory is null)
            {
                return Results.NotFound("LiveTracking-Paket wurde auf diesem System noch nicht bereitgestellt.");
            }

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                foreach (var filePath in files)
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
                    archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
                }
            }

            var fileName = $"einsatzueberwachung-livetracking-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            return Results.File(memoryStream.ToArray(), "application/zip", fileName);
        });

        return app;
    }
}
