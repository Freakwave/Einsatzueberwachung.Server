namespace Einsatzueberwachung.Domain.Models
{
    /// <summary>
    /// Einstellungen die pro Browser (localStorage) gespeichert werden.
    /// Jeder Browser hat seine eigenen Werte; die gemeinsamen Einsatzdaten
    /// bleiben auf dem Server.
    /// </summary>
    public class BrowserPreferences
    {
        // --- Theme ---
        public string ThemeMode { get; set; } = "Manual"; // "Manual" | "Auto" | "Scheduled"
        public bool IsDarkMode { get; set; } = false;
        public string DarkModeStartTime { get; set; } = "20:00"; // HH:mm
        public string DarkModeEndTime { get; set; } = "06:00";   // HH:mm
        public string ThemePreset { get; set; } = ThemePresets.Nrw;
        public string VisualIntensity { get; set; } = VisualIntensityLevels.Ausgewogen;

        // --- Sound ---
        public bool SoundAlertsEnabled { get; set; } = true;
        public int SoundVolume { get; set; } = 70; // 0-100
        public string FirstWarningSound { get; set; } = "beep";
        public string SecondWarningSound { get; set; } = "alarm";
        public int FirstWarningFrequency { get; set; } = 800;
        public int SecondWarningFrequency { get; set; } = 1200;
        public bool RepeatSecondWarning { get; set; } = true;
        public int RepeatWarningIntervalSeconds { get; set; } = 30;

        // --- Tastenkürzel ---
        public KeyboardShortcutPreferences Shortcuts { get; set; } = new();
    }

    public static class ThemePresets
    {
        public const string Nrw = "NRW";
        public const string Ruhr = "Ruhr";
    }

    public static class VisualIntensityLevels
    {
        public const string Dezent = "Dezent";
        public const string Ausgewogen = "Ausgewogen";
        public const string Lebhaft = "Lebhaft";
    }

    /// <summary>
    /// Konfigurierbare Tastenkürzel — pro Browser in localStorage gespeichert.
    /// Fehlende Keys werden bei der Deserialisierung automatisch auf Defaults gesetzt.
    /// </summary>
    public class KeyboardShortcutPreferences
    {
        public string NavHome      { get; set; } = "ctrl+h";
        public string NavKarte     { get; set; } = "ctrl+m";
        public string NavMonitor   { get; set; } = "ctrl+n";
        public string NavStart     { get; set; } = "ctrl+t";
        public string StepperUp    { get; set; } = "arrowup";
        public string StepperDown  { get; set; } = "arrowdown";
    }
}
