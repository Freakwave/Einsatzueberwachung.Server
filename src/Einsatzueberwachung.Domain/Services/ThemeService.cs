// Einsatzüberwachung - Globaler Theme Service
// Stellt sicher, dass Theme über alle Seiten/Komponenten synchron ist

using Einsatzueberwachung.Domain.Interfaces;using Microsoft.Extensions.Logging;
namespace Einsatzueberwachung.Domain.Services
{
    public class ThemeService
    {
        private readonly IMasterDataService _masterDataService;
        private readonly ILogger<ThemeService> _logger;
        private bool _isDarkMode;
        private bool _isInitialized = false;
        private System.Threading.Timer? _scheduleTimer;

        public event Action? OnThemeChanged;

        public ThemeService(IMasterDataService masterDataService, ILogger<ThemeService> logger)
        {
            _masterDataService = masterDataService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                await LoadThemeAsync();
                await CheckScheduledTheme();
                StartScheduleTimer();
                _isInitialized = true;
            }
        }

        public bool IsDarkMode => _isDarkMode;

        public string CurrentTheme => _isDarkMode ? "dark" : "light";

        public async Task LoadThemeAsync()
        {
            try
            {
                var sessionData = await _masterDataService.LoadSessionDataAsync();
                _isDarkMode = sessionData?.AppSettings?.IsDarkMode ?? false;
                _logger.LogInformation("Theme geladen: {Theme}", CurrentTheme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Themes");
                _isDarkMode = false;
            }
        }

        public async Task SetThemeAsync(bool isDark)
        {
            if (_isDarkMode != isDark)
            {
                _isDarkMode = isDark;

                // Speichere in Settings
                try
                {
                    var sessionData = await _masterDataService.LoadSessionDataAsync();
                    if (sessionData.AppSettings == null)
                        sessionData.AppSettings = new Models.AppSettings();

                    sessionData.AppSettings.IsDarkMode = isDark;
                    sessionData.AppSettings.Theme = isDark ? "Dark" : "Light";
                    await _masterDataService.SaveSessionDataAsync(sessionData);

                    _logger.LogInformation("Theme gespeichert: {Theme}", CurrentTheme);

                    // Notify subscribers
                    OnThemeChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Speichern des Themes");
                }
            }
        }

        public async Task ToggleThemeAsync()
        {
            await SetThemeAsync(!_isDarkMode);
        }

        private async Task CheckScheduledTheme()
        {
            try
            {
                var sessionData = await _masterDataService.LoadSessionDataAsync();
                if (sessionData?.AppSettings?.ThemeMode == "Scheduled")
                {
                    var now = DateTime.Now.TimeOfDay;
                    var start = sessionData.AppSettings.DarkModeStartTime;
                    var end = sessionData.AppSettings.DarkModeEndTime;

                    bool shouldBeDark;
                    if (start < end)
                    {
                        // Normal case: z.B. 09:00 - 18:00
                        shouldBeDark = now >= start && now < end;
                    }
                    else
                    {
                        // Overnight case: z.B. 20:00 - 06:00
                        shouldBeDark = now >= start || now < end;
                    }

                    if (shouldBeDark != _isDarkMode)
                    {
                        await SetThemeAsync(shouldBeDark);
                        _logger.LogInformation("Zeitgesteuerter Theme-Wechsel zu: {Theme}", CurrentTheme);
                    }
                }
                else if (sessionData?.AppSettings?.ThemeMode == "Auto")
                {
                    // Auto-Modus basierend auf Tageszeit (vereinfacht)
                    var hour = DateTime.Now.Hour;
                    bool shouldBeDark = hour < 6 || hour >= 20;
                    
                    if (shouldBeDark != _isDarkMode)
                    {
                        await SetThemeAsync(shouldBeDark);
                        _logger.LogInformation("Auto Theme-Wechsel zu: {Theme}", CurrentTheme);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei zeitgesteuertem Theme-Wechsel");
            }
        }

        private void StartScheduleTimer()
        {
            // Prüfe jede Minute ob Theme gewechselt werden muss
            _scheduleTimer = new System.Threading.Timer(
                _ => {
                    // Fire and forget - läuft asynchron
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            await CheckScheduledTheme();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Fehler im Schedule-Timer");
                        }
                    });
                },
                null,
                TimeSpan.FromSeconds(30), // Erste Prüfung nach 30 Sekunden
                TimeSpan.FromMinutes(1)   // Dann jede Minute
            );
            
            _logger.LogInformation("Schedule-Timer gestartet - prüft alle 60 Sekunden");
        }
        
        public void Dispose()
        {
            _scheduleTimer?.Dispose();
        }
    }
}
