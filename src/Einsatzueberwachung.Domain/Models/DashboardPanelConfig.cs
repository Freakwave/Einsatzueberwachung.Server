namespace Einsatzueberwachung.Domain.Models
{
    public class DashboardPanelConfig
    {
        public string PanelId { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;

        /// <summary>Breite in 12er-Grid-Spalten (3, 4, 6, 8, 9, 12)</summary>
        public int ColSpan { get; set; } = 6;

        /// <summary>Reihenfolge im Dashboard (aufsteigend = oben links nach unten rechts)</summary>
        public int Order { get; set; }

        /// <summary>Feste Höhe des Panel-Body in Pixel. 0 = automatisch (Inhalt bestimmt Höhe).</summary>
        public int PanelHeight { get; set; } = 0;
    }

    public static class KnownPanels
    {
        public const string EinsatzInfo  = "einsatz-info";
        public const string Teams        = "teams";
        public const string Notizen      = "notizen";
        public const string Wetter       = "wetter";
        public const string Suchgebiete  = "suchgebiete";
        public const string Minimap      = "minimap";
        public const string Vermissten   = "vermissten";

        public static readonly Dictionary<string, string> Labels = new()
        {
            [EinsatzInfo]  = "Einsatz-Info",
            [Teams]        = "Teams",
            [Notizen]      = "Notizen & Funk",
            [Wetter]       = "Wetter",
            [Suchgebiete]  = "Suchgebiete",
            [Minimap]      = "Karte (Mini)",
            [Vermissten]   = "Vermissteninfo",
        };
    }
}
