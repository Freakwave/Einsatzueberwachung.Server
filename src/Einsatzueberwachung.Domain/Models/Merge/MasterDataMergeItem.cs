// MasterDataMergeItem — Ein importierter Stammdaten-Eintrag mit Vorschlägen und Benutzerentscheidung

using System.Collections.Generic;

namespace Einsatzueberwachung.Domain.Models.Merge
{
    /// <summary>
    /// Gibt den Typ des importierten Stammdaten-Eintrags an.
    /// </summary>
    public enum MergeEntityType
    {
        Personal,
        Dog,
        Drone
    }

    /// <summary>
    /// Ein einzelner Kandidat-Vorschlag für die Zusammenführung mit einem lokalen Eintrag.
    /// Wird vom Vorschlags-Engine berechnet und dem Benutzer zur Auswahl präsentiert.
    /// </summary>
    public class MasterDataMergeCandidate
    {
        /// <summary>ID des lokalen Eintrags.</summary>
        public string LocalId { get; set; } = string.Empty;

        /// <summary>Anzeigename des lokalen Eintrags.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Kurzbeschreibung des Treffergrunds (z.B. "SAME_ID", "EXACT_NAME", "PARTIAL_NAME").</summary>
        public string MatchReason { get; set; } = string.Empty;

        /// <summary>Anzeigetext des Treffergrunds für die UI (z.B. "Gleiche ID", "Gleicher Name").</summary>
        public string MatchReasonLabel { get; set; } = string.Empty;

        /// <summary>Vertrauenswert zwischen 0.0 und 1.0 (höher = besserer Treffer).</summary>
        public double ConfidenceScore { get; set; }

        /// <summary>Zusätzliche Details (z.B. Skills/Spezialisierungen) für die Anzeige.</summary>
        public string DetailsDisplay { get; set; } = string.Empty;
    }

    /// <summary>
    /// Repräsentiert einen importierten Stammdaten-Eintrag im Merge-Wizard.
    /// Enthält den Originaleintrag, eine Liste gerankter Vorschläge und die Benutzerentscheidung.
    /// </summary>
    public class MasterDataMergeItem
    {
        /// <summary>ID des importierten Eintrags (aus dem SubgroupExportPacket).</summary>
        public string ImportedId { get; set; } = string.Empty;

        /// <summary>Anzeigename des importierten Eintrags.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Details (z.B. "Skills: HF, EH") für die Anzeige.</summary>
        public string DetailsDisplay { get; set; } = string.Empty;

        /// <summary>Typ: Personal, Dog oder Drone.</summary>
        public MergeEntityType EntityType { get; set; }

        /// <summary>Der original importierte Eintrag (PersonalEntry, DogEntry oder DroneEntry).</summary>
        public object ImportedEntry { get; set; } = null!;

        /// <summary>Gerankte Vorschläge (bester zuerst).</summary>
        public List<MasterDataMergeCandidate> Suggestions { get; set; } = new();

        /// <summary>Entscheidung des Benutzers (null = noch nicht entschieden).</summary>
        public MergeDecision Decision { get; set; } = MergeDecision.Undecided;

        /// <summary>
        /// Lokale ID des gewählten Eintrags (nur bei Decision = LinkToExisting).
        /// </summary>
        public string? SelectedLocalId { get; set; }

        /// <summary>
        /// Ob die lokalen Felder (Name, Skills, Notizen) mit den importierten Werten überschrieben werden sollen.
        /// Nur relevant bei Decision = LinkToExisting.
        /// </summary>
        public bool OverwriteLocalFields { get; set; } = false;

        /// <summary>Gibt an, ob der Benutzer bereits eine Entscheidung getroffen hat.</summary>
        public bool IsResolved => Decision != MergeDecision.Undecided;
    }
}
