using System;
using System.IO;

namespace Einsatzueberwachung.Domain.Services
{
    public static class AppPathResolver
    {
        private const string ApplicationName = "Einsatzueberwachung.Server";
        private const string DataDirectoryEnvironmentVariable = "EINSATZUEBERWACHUNG_DATA_DIR";
        private const string ReportDirectoryEnvironmentVariable = "EINSATZUEBERWACHUNG_REPORT_DIR";

        public static string GetDataDirectory()
        {
            var configuredPath = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
            var basePath = !string.IsNullOrWhiteSpace(configuredPath)
                ? configuredPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName, "data");

            return EnsureDirectory(basePath);
        }

        public static string GetArchiveDirectory()
        {
            return EnsureDirectory(Path.Combine(GetDataDirectory(), "archiv"));
        }

        public static string GetReportDirectory()
        {
            var configuredPath = Environment.GetEnvironmentVariable(ReportDirectoryEnvironmentVariable);
            var basePath = !string.IsNullOrWhiteSpace(configuredPath)
                ? configuredPath
                : Path.Combine(GetDataDirectory(), "berichte");

            return EnsureDirectory(basePath);
        }

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}