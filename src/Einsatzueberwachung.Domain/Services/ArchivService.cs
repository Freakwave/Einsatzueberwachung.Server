// Archiv-Service - Speichert und verwaltet abgeschlossene Einsaetze

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public class ArchivService : IArchivService
    {
        private readonly string _archivDirectory;
        private readonly string _archivFilePath;
        private List<ArchivedEinsatz> _archiv = new();
        private bool _isLoaded = false;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ArchivService()
        {
            // Speichert im AppData-Ordner
            _archivDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Einsatzueberwachung",
                "Archiv");
            
            _archivFilePath = Path.Combine(_archivDirectory, "einsatz_archiv.json");
            
            // Stelle sicher, dass der Ordner existiert
            if (!Directory.Exists(_archivDirectory))
            {
                Directory.CreateDirectory(_archivDirectory);
            }
        }

        private async Task EnsureLoadedAsync()
        {
            if (_isLoaded) return;

            if (File.Exists(_archivFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_archivFilePath);
                    _archiv = JsonSerializer.Deserialize<List<ArchivedEinsatz>>(json, JsonOptions) ?? new();
                }
                catch (Exception)
                {
                    _archiv = new();
                }
            }

            _isLoaded = true;
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_archiv, JsonOptions);
            await File.WriteAllTextAsync(_archivFilePath, json);
        }

        public async Task<ArchivedEinsatz> ArchiveEinsatzAsync(EinsatzData einsatzData, string ergebnis, string bemerkungen)
        {
            await EnsureLoadedAsync();

            var archived = ArchivedEinsatz.FromEinsatzData(einsatzData, ergebnis, bemerkungen);
            _archiv.Insert(0, archived); // Neueste zuerst
            
            await SaveAsync();
            
            return archived;
        }

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

            // Textsuche
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

            // Datumsfilter
            if (criteria.VonDatum.HasValue)
            {
                query = query.Where(e => e.EinsatzDatum >= criteria.VonDatum.Value);
            }

            if (criteria.BisDatum.HasValue)
            {
                query = query.Where(e => e.EinsatzDatum <= criteria.BisDatum.Value.AddDays(1));
            }

            // Einsatz/Uebung Filter
            if (criteria.NurEinsaetze.HasValue)
            {
                query = query.Where(e => e.IstEinsatz == criteria.NurEinsaetze.Value);
            }

            // Ergebnis-Filter
            if (!string.IsNullOrWhiteSpace(criteria.Ergebnis))
            {
                query = query.Where(e => e.Ergebnis.Equals(criteria.Ergebnis, StringComparison.OrdinalIgnoreCase));
            }

            // Einsatzort-Filter
            if (!string.IsNullOrWhiteSpace(criteria.Einsatzort))
            {
                query = query.Where(e => e.Einsatzort.ToLowerInvariant().Contains(criteria.Einsatzort.ToLowerInvariant()));
            }

            return query.OrderByDescending(e => e.EinsatzDatum).ToList();
        }

        public async Task<bool> DeleteAsync(string id)
        {
            await EnsureLoadedAsync();

            var einsatz = _archiv.FirstOrDefault(e => e.Id == id);
            if (einsatz == null) return false;

            _archiv.Remove(einsatz);
            await SaveAsync();

            return true;
        }

        public async Task<byte[]> ExportAllAsJsonAsync()
        {
            await EnsureLoadedAsync();

            var exportData = new
            {
                ExportDatum = DateTime.Now,
                Version = "3.12.0",
                AnzahlEinsaetze = _archiv.Count,
                Einsaetze = _archiv
            };

            var json = JsonSerializer.Serialize(exportData, JsonOptions);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public async Task<int> ImportFromJsonAsync(byte[] jsonData)
        {
            await EnsureLoadedAsync();

            var json = System.Text.Encoding.UTF8.GetString(jsonData);
            
            try
            {
                // Versuche als Export-Format zu parsen
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                List<ArchivedEinsatz>? importedList = null;

                if (root.TryGetProperty("einsaetze", out var einsaetzeElement))
                {
                    importedList = JsonSerializer.Deserialize<List<ArchivedEinsatz>>(einsaetzeElement.GetRawText(), JsonOptions);
                }
                else
                {
                    // Versuche als einfache Liste
                    importedList = JsonSerializer.Deserialize<List<ArchivedEinsatz>>(json, JsonOptions);
                }

                if (importedList == null || importedList.Count == 0)
                    return 0;

                int imported = 0;
                foreach (var einsatz in importedList)
                {
                    // Pruefen ob bereits vorhanden (nach ID)
                    if (!_archiv.Any(e => e.Id == einsatz.Id))
                    {
                        _archiv.Add(einsatz);
                        imported++;
                    }
                }

                if (imported > 0)
                {
                    await SaveAsync();
                }

                return imported;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<ArchivStatistics> GetStatisticsAsync()
        {
            await EnsureLoadedAsync();

            var stats = new ArchivStatistics
            {
                GesamtAnzahl = _archiv.Count,
                AnzahlEinsaetze = _archiv.Count(e => e.IstEinsatz),
                AnzahlUebungen = _archiv.Count(e => !e.IstEinsatz),
                AnzahlDiesesJahr = _archiv.Count(e => e.EinsatzDatum.Year == DateTime.Now.Year),
                AnzahlDiesenMonat = _archiv.Count(e => 
                    e.EinsatzDatum.Year == DateTime.Now.Year && 
                    e.EinsatzDatum.Month == DateTime.Now.Month),
                GesamtPersonalEinsaetze = _archiv.Sum(e => e.AnzahlPersonal),
                GesamtHundeEinsaetze = _archiv.Sum(e => e.AnzahlHunde)
            };

            // Durchschnittliche Dauer berechnen
            var einsaetzeMitDauer = _archiv.Where(e => e.Dauer.HasValue).ToList();
            if (einsaetzeMitDauer.Any())
            {
                var totalMinutes = einsaetzeMitDauer.Sum(e => e.Dauer!.Value.TotalMinutes);
                stats.DurchschnittlicheDauer = TimeSpan.FromMinutes(totalMinutes / einsaetzeMitDauer.Count);
            }

            // Haeufigster Erfolgstyp
            var ergebnisGruppen = _archiv
                .Where(e => !string.IsNullOrEmpty(e.Ergebnis))
                .GroupBy(e => e.Ergebnis)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            
            if (ergebnisGruppen != null)
            {
                stats.HaeufigsterErfolgTyp = ergebnisGruppen.Key;
            }

            // Einsaetze pro Monat (letztes Jahr)
            var letzteJahr = DateTime.Now.AddYears(-1);
            var proMonat = _archiv
                .Where(e => e.EinsatzDatum >= letzteJahr)
                .GroupBy(e => e.EinsatzDatum.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());
            stats.EinsaetzeProMonat = proMonat;

            // Einsaetze pro Jahr
            var proJahr = _archiv
                .GroupBy(e => e.EinsatzDatum.Year.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
            stats.EinsaetzeProJahr = proJahr;

            return stats;
        }
    }
}
