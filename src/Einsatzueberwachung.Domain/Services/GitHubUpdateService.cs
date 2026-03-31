using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Einsatzueberwachung.Domain.Services
{
    /// <summary>
    /// Service für die Verwaltung von GitHub-Release Updates
    /// Prüft GitHub auf neue Releases und verwaltet Update-Prozesse
    /// </summary>
    public class GitHubUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubUpdateService> _logger;
        
        // GitHub API Konfiguration
        private const string GITHUB_OWNER = "Elemirus1996";
        private const string GITHUB_REPO = "Einsatzueberwachung.Web";
        private const string GITHUB_API_URL = "https://api.github.com/repos/{0}/{1}/releases/latest";
        
        public string CurrentVersion { get; set; } = "4.3.4";
        public UpdateCheckResult? LastCheckResult { get; set; }

        public GitHubUpdateService(HttpClient httpClient, ILogger<GitHubUpdateService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // User-Agent für GitHub API erforderlich
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Einsatzueberwachung-Update-Checker");
        }

        /// <summary>
        /// Prüft GitHub auf neue Releases
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var url = string.Format(GITHUB_API_URL, GITHUB_OWNER, GITHUB_REPO);
                _logger.LogInformation("Prüfe GitHub auf Updates: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub API Fehler: {StatusCode}", response.StatusCode);
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"GitHub API Fehler: {response.StatusCode}",
                        CheckedAt = DateTime.Now
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var tagName = root.GetProperty("tag_name").GetString() ?? "unknown";
                var version = tagName.TrimStart('v');
                var downloadUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
                var releaseNotes = root.GetProperty("body").GetString() ?? string.Empty;

                // Assets (Dateien) aus dem Release holen
                var assets = root.GetProperty("assets");
                string? installerUrl = null;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                        name.Contains("Installer", StringComparison.OrdinalIgnoreCase))
                    {
                        installerUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                var result = new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = CurrentVersion,
                    LatestVersion = version,
                    ReleaseUrl = downloadUrl,
                    InstallerUrl = installerUrl,
                    ReleaseNotes = releaseNotes,
                    CheckedAt = DateTime.Now,
                    UpdateAvailable = IsNewerVersion(version, CurrentVersion)
                };

                LastCheckResult = result;
                
                if (result.UpdateAvailable)
                {
                    _logger.LogInformation("Update verfügbar: {Current} → {Latest}", 
                        CurrentVersion, version);
                }
                else
                {
                    _logger.LogInformation("Anwendung ist aktuell");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Prüfen auf Updates");
                return new UpdateCheckResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    CheckedAt = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Vergleicht zwei Versionsnummern
        /// </summary>
        private bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = Version.Parse(latestVersion);
                var current = Version.Parse(currentVersion);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lädt die Installer-Datei herunter
        /// </summary>
        public async Task<byte[]?> DownloadInstallerAsync(string url)
        {
            try
            {
                _logger.LogInformation("Downloade Installer: {Url}", url);
                var data = await _httpClient.GetByteArrayAsync(url);
                _logger.LogInformation("Installer erfolgreich heruntergeladen: {Size} bytes", data.Length);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Download des Installers");
                return null;
            }
        }
    }

    /// <summary>
    /// Ergebnis einer Update-Prüfung
    /// </summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string? InstallerUrl { get; set; }
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime CheckedAt { get; set; }
        public bool UpdateAvailable { get; set; }
        public string? ErrorMessage { get; set; }
    }
}




