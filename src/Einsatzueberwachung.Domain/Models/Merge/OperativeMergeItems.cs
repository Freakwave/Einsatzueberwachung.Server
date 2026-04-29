// Merge-Elemente für operative Daten (Teams, Notizen, Suchgebiete)

using System.Collections.Generic;

namespace Einsatzueberwachung.Domain.Models.Merge
{
    /// <summary>
    /// Ein importiertes Team im Merge-Wizard.
    /// </summary>
    public class TeamMergeItem
    {
        /// <summary>Das importierte Team (IDs bereits über die Remapping-Tabelle aufgelöst).</summary>
        public Team ImportedTeam { get; set; } = null!;

        /// <summary>Gibt an, ob lokal bereits ein Team mit derselben TeamId existiert.</summary>
        public bool HasLocalConflict { get; set; }

        /// <summary>Das lokale Team bei ID-Konflikt (zur Darstellung im Diff).</summary>
        public Team? LocalConflictTeam { get; set; }

        /// <summary>Ob dieses Team in den Haupt-Einsatz übernommen werden soll.</summary>
        public bool ShouldImport { get; set; } = true;

        /// <summary>Anzeigename der aufgelösten Mitglieder (nach ID-Remapping).</summary>
        public List<string> ResolvedMemberNames { get; set; } = new();
    }

    /// <summary>
    /// Eine importierte Notiz / ein Funkspruch im Merge-Wizard.
    /// </summary>
    public class NoteMergeItem
    {
        /// <summary>Die importierte Notiz.</summary>
        public GlobalNotesEntry Note { get; set; } = null!;

        /// <summary>True, wenn lokal bereits ein Eintrag mit derselben ID existiert (überspringen).</summary>
        public bool IsAlreadyPresent { get; set; }

        /// <summary>Ob diese Notiz importiert werden soll (nur für neue Einträge relevant).</summary>
        public bool ShouldImport { get; set; } = true;
    }

    /// <summary>
    /// Ein importiertes Suchgebiet im Merge-Wizard.
    /// </summary>
    public class SearchAreaMergeItem
    {
        /// <summary>Das importierte Suchgebiet (AssignedTeamId wurde bereits per ID-Remapping aufgelöst).</summary>
        public SearchArea ImportedArea { get; set; } = null!;

        /// <summary>Art des Konflikts (falls vorhanden).</summary>
        public SearchAreaConflict ConflictType { get; set; } = SearchAreaConflict.None;

        /// <summary>Das lokale Gebiet, das den Konflikt verursacht.</summary>
        public SearchArea? LocalConflictArea { get; set; }

        /// <summary>Wie ein Namenskonflikt aufgelöst wird (nur bei ConflictType = SameNameDifferentId).</summary>
        public SearchAreaNameConflictResolution NameConflictResolution { get; set; } = SearchAreaNameConflictResolution.KeepBoth;

        /// <summary>Ob dieses Gebiet importiert werden soll.</summary>
        public bool ShouldImport { get; set; } = true;
    }

    /// <summary>
    /// Typ eines Konflikts bei einem Suchgebiet.
    /// </summary>
    public enum SearchAreaConflict
    {
        /// <summary>Kein Konflikt — Gebiet kann direkt übernommen werden.</summary>
        None,

        /// <summary>Exakt dieselbe ID existiert bereits lokal — wird automatisch übersprungen.</summary>
        SameIdExists,

        /// <summary>Gleicher Name, andere ID — Benutzerentscheidung erforderlich.</summary>
        SameNameDifferentId
    }
}
