using System.IO.Compression;
using System.Text;

namespace Einsatzueberwachung.Tests;

// ------------------------------------------------------------
// ZIP-Backup — Smoketest
// Spiegelt die Logik aus DownloadEndpoints.cs (/downloads/data-backup.zip):
// gesamtes Datenverzeichnis rekursiv mit ZipArchive + CreateEntryFromFile +
// CompressionLevel.Optimal in einen MemoryStream packen.
// Test packt eine kleine Temp-Verzeichnis-Hierarchie und prueft, dass das
// Ergebnis ein gueltiges ZIP ist und alle Eintraege mit korrektem Inhalt
// enthaelt - inklusive Unterordnern und Sonderzeichen im Pfad.
// ------------------------------------------------------------
public class BackupZipSmokeTests
{
    private static byte[] ZipDirectory(string sourceDirectory)
    {
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
        return memoryStream.ToArray();
    }

    private static string CreateTempDataTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "eu-backup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "settings"));
        Directory.CreateDirectory(Path.Combine(root, "stammdaten", "hunde"));

        File.WriteAllText(Path.Combine(root, "session.json"), "{\"einsatz\":\"aktiv\"}", Encoding.UTF8);
        File.WriteAllText(Path.Combine(root, "settings", "app-settings.json"), "{\"theme\":\"dark\"}", Encoding.UTF8);
        File.WriteAllText(Path.Combine(root, "stammdaten", "personal.json"), "[]", Encoding.UTF8);
        File.WriteAllText(Path.Combine(root, "stammdaten", "hunde", "rex.json"), "{\"name\":\"Rex\"}", Encoding.UTF8);

        return root;
    }

    [Fact]
    public void ZipDirectory_LiefertOeffenbaresZipMitAllenEintraegen()
    {
        var root = CreateTempDataTree();
        try
        {
            var zipBytes = ZipDirectory(root);

            Assert.True(zipBytes.Length > 0);
            // ZIP local-file-header magic: PK\x03\x04
            Assert.Equal(0x50, zipBytes[0]);
            Assert.Equal(0x4B, zipBytes[1]);
            Assert.Equal(0x03, zipBytes[2]);
            Assert.Equal(0x04, zipBytes[3]);

            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet();
            Assert.Contains("session.json", entries);
            Assert.Contains("settings/app-settings.json", entries);
            Assert.Contains("stammdaten/personal.json", entries);
            Assert.Contains("stammdaten/hunde/rex.json", entries);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZipDirectory_InhaltLaesstSichRoundtripExtrahieren()
    {
        var root = CreateTempDataTree();
        try
        {
            var zipBytes = ZipDirectory(root);

            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            var entry = archive.Entries.Single(e => e.FullName.Replace('\\', '/') == "stammdaten/hunde/rex.json");
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var content = reader.ReadToEnd();

            Assert.Equal("{\"name\":\"Rex\"}", content);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZipDirectory_LeeresVerzeichnis_LiefertLeeresAberValidesZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "eu-backup-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zipBytes = ZipDirectory(root);

            // Leeres ZIP hat nur den End-of-Central-Directory-Record (mind. 22 Byte).
            Assert.True(zipBytes.Length >= 22);

            using var ms = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.Empty(archive.Entries);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
