// SubgroupMergeSession — Transiente In-Memory-Zustandsverwaltung für den Merge-Wizard
// Wird NICHT persistiert. Lebt nur für die Dauer einer Wizard-Sitzung.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Einsatzueberwachung.Domain.Models.Merge
{
    /// <summary>
    /// Schritt im Merge-Wizard.
    /// </summary>
    public enum MergeWizardStep
    {
        Upload = 1,
        MasterDataResolution = 2,
        OperationalDataReview = 3,
        Confirmation = 4,
        Result = 5
    }

    /// <summary>
    /// Hält den vollständigen Zustand einer laufenden Teilgruppen-Zusammenführung.
    /// Transient (nur im Speicher) — wird nie persistiert.
    /// </summary>
    public class SubgroupMergeSession
    {
        /// <summary>Eindeutige Session-ID.</summary>
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Das importierte Export-Paket der Teilgruppe.</summary>
        public SubgroupExportPacket Packet { get; set; } = null!;

        /// <summary>Gibt an, ob eine EinsatzNummer-Abweichung vom Benutzer bestätigt wurde.</summary>
        public bool EinsatzNrMismatchAcknowledged { get; set; } = false;

        /// <summary>Aktueller Wizard-Schritt.</summary>
        public MergeWizardStep CurrentStep { get; set; } = MergeWizardStep.Upload;

        // === Schritt 2: Stammdaten-Auflösung ===

        /// <summary>Merge-Elemente für Personal-Einträge.</summary>
        public List<MasterDataMergeItem> PersonalItems { get; set; } = new();

        /// <summary>Merge-Elemente für Hunde-Einträge.</summary>
        public List<MasterDataMergeItem> DogItems { get; set; } = new();

        /// <summary>Merge-Elemente für Drohnen-Einträge.</summary>
        public List<MasterDataMergeItem> DroneItems { get; set; } = new();

        /// <summary>
        /// ID-Remapping-Tabelle: importierte ID → aufgelöste lokale ID.
        /// Wird in Schritt 2 aufgebaut und in den Folgeschritten genutzt.
        /// </summary>
        public Dictionary<string, string> IdRemapping { get; set; } = new();

        // === Schritt 3: Operative Daten ===

        /// <summary>Merge-Elemente für Teams.</summary>
        public List<TeamMergeItem> TeamItems { get; set; } = new();

        /// <summary>Merge-Elemente für Notizen / Funksprüche.</summary>
        public List<NoteMergeItem> NoteItems { get; set; } = new();

        /// <summary>Merge-Elemente für Suchgebiete.</summary>
        public List<SearchAreaMergeItem> SearchAreaItems { get; set; } = new();

        /// <summary>Anzahl neuer GPS-Track-Snapshots (rein additiv, keine Einzelentscheidung nötig).</summary>
        public int NewTrackSnapshotCount { get; set; }

        /// <summary>Anzahl neuer Karten-Marker (rein additiv, keine Einzelentscheidung nötig).</summary>
        public int NewMarkerCount { get; set; }

        /// <summary>
        /// Ziel: null = aktiver Einsatz, sonst ID des archivierten Einsatzes.
        /// </summary>
        public string? TargetArchivedEinsatzId { get; set; }

        // === Schritt 5: Ergebnis ===

        /// <summary>Der Protokolleintrag nach erfolgreicher Zusammenführung.</summary>
        public MergeHistoryEntry? AppliedMerge { get; set; }

        // === Computed helpers ===

        /// <summary>Alle Stammdaten-Items über alle Typen.</summary>
        public IEnumerable<MasterDataMergeItem> AllMasterDataItems =>
            PersonalItems.Concat(DogItems).Concat(DroneItems);

        /// <summary>Anzahl bereits entschiedener Stammdaten-Items.</summary>
        public int ResolvedMasterDataCount => AllMasterDataItems.Count(i => i.IsResolved);

        /// <summary>Gesamtzahl der Stammdaten-Items.</summary>
        public int TotalMasterDataCount => AllMasterDataItems.Count();

        /// <summary>True, wenn alle Stammdaten-Items entschieden sind.</summary>
        public bool AllMasterDataResolved =>
            TotalMasterDataCount == 0 || ResolvedMasterDataCount == TotalMasterDataCount;

        /// <summary>Anzahl der Teams, die importiert werden sollen.</summary>
        public int TeamsToImport => TeamItems.Count(t => t.ShouldImport);

        /// <summary>Anzahl der Notizen, die importiert werden sollen.</summary>
        public int NotesToImport => NoteItems.Count(n => !n.IsAlreadyPresent && n.ShouldImport);

        /// <summary>Anzahl der Suchgebiete, die importiert werden sollen.</summary>
        public int SearchAreasToImport =>
            SearchAreaItems.Count(a => a.ShouldImport && a.ConflictType != SearchAreaConflict.SameIdExists);
    }
}
