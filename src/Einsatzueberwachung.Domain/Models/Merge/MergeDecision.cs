// Entscheidungsoptionen für einen einzelnen Import-Eintrag bei der Zusammenführung

namespace Einsatzueberwachung.Domain.Models.Merge
{
    /// <summary>
    /// Gibt an, welche Aktion der Benutzer für einen importierten Stammdaten-Eintrag gewählt hat.
    /// </summary>
    public enum MergeDecision
    {
        /// <summary>Noch keine Entscheidung getroffen (Initialzustand).</summary>
        Undecided = 0,

        /// <summary>Den importierten Eintrag mit einem lokalen Eintrag verknüpfen.
        /// Alle Referenzen aus dem Import werden auf die lokale ID umgeschrieben.</summary>
        LinkToExisting = 1,

        /// <summary>Den importierten Eintrag als neuen Datensatz in den lokalen Stammdaten anlegen.</summary>
        CreateNew = 2,

        /// <summary>Den Eintrag ignorieren. Referenzen in Teams werden geleert (mit Warnung angezeigt).</summary>
        Skip = 3
    }

    /// <summary>
    /// Entscheidungsoptionen für einen Namenskonflikt bei Suchgebieten (gleicher Name, andere ID).
    /// </summary>
    public enum SearchAreaNameConflictResolution
    {
        /// <summary>Import-Gebiet umbenennen (Suffix "_import" anhängen).</summary>
        Rename = 0,

        /// <summary>Lokales Gebiet durch das importierte ersetzen.</summary>
        ReplaceLocal = 1,

        /// <summary>Beide Einträge behalten (Import erhält neue ID).</summary>
        KeepBoth = 2
    }
}
