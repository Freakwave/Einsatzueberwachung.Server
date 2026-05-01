using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class ArchivService
    {
        public async Task<List<ArchivedEinsatz>> GetAllArchivedAsync()
        {
            await EnsureLoadedAsync();
            return _archiv.OrderByDescending(e => e.EinsatzDatum).ToList();
        }

        public async Task<ArchivedEinsatz?> GetByIdAsync(string id)
        {
            await EnsureLoadedAsync();
            return _archiv.FirstOrDefault(e => e.Id == id);
        }

        public async Task<List<ArchivedEinsatz>> SearchAsync(ArchivSearchCriteria criteria)
        {
            await EnsureLoadedAsync();

            var query = _archiv.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(criteria.Suchtext))
            {
                var suchtext = criteria.Suchtext.ToLowerInvariant();
                query = query.Where(e =>
                    e.Einsatzort.ToLowerInvariant().Contains(suchtext) ||
                    e.Einsatzleiter.ToLowerInvariant().Contains(suchtext) ||
                    e.EinsatzNummer.ToLowerInvariant().Contains(suchtext) ||
                    e.Bemerkungen.ToLowerInvariant().Contains(suchtext) ||
                    e.Ergebnis.ToLowerInvariant().Contains(suchtext));
            }

            if (criteria.VonDatum.HasValue)
            {
                query = query.Where(e => e.EinsatzDatum >= criteria.VonDatum.Value);
            }

            if (criteria.BisDatum.HasValue)
            {
                query = query.Where(e => e.EinsatzDatum <= criteria.BisDatum.Value.AddDays(1));
            }

            if (criteria.NurEinsaetze.HasValue)
            {
                query = query.Where(e => e.IstEinsatz == criteria.NurEinsaetze.Value);
            }

            if (!string.IsNullOrWhiteSpace(criteria.Ergebnis))
            {
                query = query.Where(e => e.Ergebnis.Equals(criteria.Ergebnis, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(criteria.Einsatzort))
            {
                query = query.Where(e => e.Einsatzort.ToLowerInvariant().Contains(criteria.Einsatzort.ToLowerInvariant()));
            }

            if (!string.IsNullOrWhiteSpace(criteria.Einsatzleiter))
            {
                query = query.Where(e => e.Einsatzleiter.ToLowerInvariant().Contains(criteria.Einsatzleiter.ToLowerInvariant()));
            }

            return query.OrderByDescending(e => e.EinsatzDatum).ToList();
        }

        public async Task<ArchivStatistics> GetStatisticsAsync()
        {
            await EnsureLoadedAsync();

            var stats = new ArchivStatistics
            {
                GesamtAnzahl = _archiv.Count,
                AnzahlEinsaetze = _archiv.Count(e => e.IstEinsatz),
                AnzahlUebungen = _archiv.Count(e => !e.IstEinsatz),
                AnzahlDiesesJahr = _archiv.Count(e => e.EinsatzDatum.Year == Now.Year),
                AnzahlDiesenMonat = _archiv.Count(e =>
                    e.EinsatzDatum.Year == Now.Year &&
                    e.EinsatzDatum.Month == Now.Month),
                GesamtPersonalEinsaetze = _archiv.Sum(e => e.AnzahlPersonal),
                GesamtHundeEinsaetze = _archiv.Sum(e => e.AnzahlHunde)
            };

            var einsaetzeMitDauer = _archiv.Where(e => e.Dauer.HasValue).ToList();
            if (einsaetzeMitDauer.Any())
            {
                var totalMinutes = einsaetzeMitDauer.Sum(e => e.Dauer!.Value.TotalMinutes);
                stats.DurchschnittlicheDauer = TimeSpan.FromMinutes(totalMinutes / einsaetzeMitDauer.Count);
            }

            var ergebnisGruppen = _archiv
                .Where(e => !string.IsNullOrWhiteSpace(e.Ergebnis))
                .GroupBy(e => e.Ergebnis)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (ergebnisGruppen != null)
            {
                stats.HaeufigsterErfolgTyp = ergebnisGruppen.Key;
            }

            var letzteJahr = Now.AddYears(-1);
            var proMonat = _archiv
                .Where(e => e.EinsatzDatum >= letzteJahr)
                .GroupBy(e => e.EinsatzDatum.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            stats.EinsaetzeProMonat = proMonat;

            var proJahr = _archiv
                .GroupBy(e => e.EinsatzDatum.Year.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
            stats.EinsaetzeProJahr = proJahr;

            return stats;
        }
    }
}
