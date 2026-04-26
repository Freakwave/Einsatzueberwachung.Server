// Archiv-Datenmodelle fuer Suche und Statistiken

using System;
using System.Collections.Generic;

namespace Einsatzueberwachung.Domain.Models
{
    /// <summary>
    /// Suchkriterien fuer das Archiv
    /// </summary>
    public class ArchivSearchCriteria
    {
        public string? Suchtext { get; set; }
        public DateTime? VonDatum { get; set; }
        public DateTime? BisDatum { get; set; }
        public bool? NurEinsaetze { get; set; } // true = nur Einsaetze, false = nur Uebungen, null = alle
        public string? Ergebnis { get; set; }
        public string? Einsatzort { get; set; }
    }

    /// <summary>
    /// Statistiken ueber das Archiv
    /// </summary>
    public class ArchivStatistics
    {
        public int GesamtAnzahl { get; set; }
        public int AnzahlEinsaetze { get; set; }
        public int AnzahlUebungen { get; set; }
        public int AnzahlDiesesJahr { get; set; }
        public int AnzahlDiesenMonat { get; set; }
        public TimeSpan DurchschnittlicheDauer { get; set; }
        public string HaeufigsterErfolgTyp { get; set; } = string.Empty;
        public int GesamtPersonalEinsaetze { get; set; }
        public int GesamtHundeEinsaetze { get; set; }
        public Dictionary<string, int> EinsaetzeProMonat { get; set; } = new();
        public Dictionary<string, int> EinsaetzeProJahr { get; set; } = new();
    }
}
