// EinsatzExportService — Erstellt ein portables Export-Paket aus dem laufenden Einsatz

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Services
{
    /// <summary>
    /// Implementiert den Einsatz-Export aus dem aktiven Einsatz.
    /// </summary>
    public class EinsatzExportService : IEinsatzExportService
    {
        private readonly IEinsatzService _einsatzService;
        private readonly IMasterDataService _masterDataService;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null   // PascalCase – identisch mit Import-Erwartung
        };

        public EinsatzExportService(
            IEinsatzService einsatzService,
            IMasterDataService masterDataService)
        {
            _einsatzService = einsatzService;
            _masterDataService = masterDataService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuildExportPacketAsync
        // ─────────────────────────────────────────────────────────────────────

        public async Task<EinsatzExportPacket> BuildExportPacketAsync(
            IEnumerable<string> selectedTeamIds,
            string label,
            EinsatzExportOptions? options = null)
        {
            options ??= new EinsatzExportOptions();

            var teamIdSet = new HashSet<string>(selectedTeamIds, StringComparer.OrdinalIgnoreCase);
            var selectedTeams = _einsatzService.Teams
                .Where(t => teamIdSet.Contains(t.TeamId))
                .ToList();

            var einsatz = _einsatzService.CurrentEinsatz;

            var packet = new EinsatzExportPacket
            {
                Label = label.Trim(),
                ExportedAt = DateTime.UtcNow,
                EinsatzNummer = einsatz.EinsatzNummer
            };

            // ── 1. Teams ──
            packet.Teams.AddRange(selectedTeams);

            // ── 2. Stammdaten (nur referenzierte Einträge) ──
            await CollectStammdatenAsync(packet, selectedTeams);

            // ── 3. Notizen / Funksprüche ──
            CollectNotes(packet, teamIdSet, einsatz.GlobalNotesEntries ?? new(), options);

            // ── 4. Suchgebiete ──
            CollectSearchAreas(packet, teamIdSet, einsatz.SearchAreas ?? new(), options);

            // ── 5. Karten-Marker ──
            if (options.IncludeMapMarkers)
            {
                var markers = await _einsatzService.GetMapMarkersAsync();
                packet.MapMarkers.AddRange(markers);
            }

            // ── 6. GPS-Tracks ──
            if (options.IncludeTrackSnapshots)
            {
                CollectTrackSnapshots(packet, selectedTeams, teamIdSet, einsatz);
            }

            return packet;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Serialize / GetFileName
        // ─────────────────────────────────────────────────────────────────────

        public byte[] Serialize(EinsatzExportPacket packet)
        {
            var json = JsonSerializer.Serialize(packet, JsonOptions);
            return Encoding.UTF8.GetBytes(json);
        }

        public string GetFileName(EinsatzExportPacket packet)
        {
            var safeNr = MakeSafeFileName(packet.EinsatzNummer);
            var timestamp = packet.ExportedAt.ToLocalTime().ToString("yyyyMMdd_HHmm");

            if (!string.IsNullOrWhiteSpace(packet.Label))
            {
                var safeName = MakeSafeFileName(packet.Label);
                return $"{safeName}_{safeNr}_{timestamp}.einsatz-export.json";
            }

            return $"export_{safeNr}_{timestamp}.einsatz-export.json";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task CollectStammdatenAsync(EinsatzExportPacket packet, List<Team> teams)
        {
            var personalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var droneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var team in teams)
            {
                if (!string.IsNullOrEmpty(team.HundefuehrerId)) personalIds.Add(team.HundefuehrerId);
                if (!string.IsNullOrEmpty(team.HelferId))       personalIds.Add(team.HelferId);
                if (!string.IsNullOrEmpty(team.DogId))          dogIds.Add(team.DogId);
                if (!string.IsNullOrEmpty(team.DroneId))        droneIds.Add(team.DroneId);
            }

            // Personal
            if (personalIds.Any())
            {
                var all = await _masterDataService.GetPersonalListAsync();
                packet.Personal.AddRange(all.Where(p => personalIds.Contains(p.Id)));
            }

            // Hunde (und ggf. ihre Führerpersonen, falls noch nicht enthalten)
            if (dogIds.Any())
            {
                var allDogs = await _masterDataService.GetDogListAsync();
                var dogs = allDogs.Where(d => dogIds.Contains(d.Id)).ToList();
                packet.Dogs.AddRange(dogs);

                var existingPersonalIds = new HashSet<string>(
                    packet.Personal.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);

                // Hundeführer aus DogEntry.HundefuehrerIds einbinden
                var additionalPersonalIds = dogs
                    .SelectMany(d => d.HundefuehrerIds ?? new())
                    .Where(id => !existingPersonalIds.Contains(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (additionalPersonalIds.Any())
                {
                    var allPersonal = await _masterDataService.GetPersonalListAsync();
                    foreach (var id in additionalPersonalIds)
                    {
                        var p = allPersonal.FirstOrDefault(x => x.Id == id);
                        if (p != null)
                        {
                            packet.Personal.Add(p);
                            existingPersonalIds.Add(id);
                        }
                    }
                }
            }

            // Drohnen
            if (droneIds.Any())
            {
                var allDrones = await _masterDataService.GetDroneListAsync();
                packet.Drones.AddRange(allDrones.Where(d => droneIds.Contains(d.Id)));
            }
        }

        private static void CollectNotes(
            EinsatzExportPacket packet,
            HashSet<string> teamIdSet,
            List<GlobalNotesEntry> allNotes,
            EinsatzExportOptions options)
        {
            foreach (var note in allNotes)
            {
                // System-Notizen überspringen, wenn nicht gewünscht
                if (!options.IncludeSystemNotes &&
                    note.Type != GlobalNotesEntryType.Manual &&
                    note.Type != GlobalNotesEntryType.EinsatzUpdate)
                {
                    continue;
                }

                bool include = false;

                if (options.IncludeGlobalNotes)
                {
                    include = true;
                }
                else if (options.IncludeTeamNotes && !string.IsNullOrEmpty(note.SourceTeamId) &&
                         teamIdSet.Contains(note.SourceTeamId))
                {
                    include = true;
                }

                if (include)
                    packet.Notes.Add(note);
            }
        }

        private static void CollectSearchAreas(
            EinsatzExportPacket packet,
            HashSet<string> teamIdSet,
            List<SearchArea> allAreas,
            EinsatzExportOptions options)
        {
            foreach (var area in allAreas)
            {
                if (options.IncludeAllSearchAreas)
                {
                    packet.SearchAreas.Add(area);
                }
                else if (options.IncludeAssignedSearchAreas &&
                         !string.IsNullOrEmpty(area.AssignedTeamId) &&
                         teamIdSet.Contains(area.AssignedTeamId))
                {
                    packet.SearchAreas.Add(area);
                }
            }
        }

        private static void CollectTrackSnapshots(
            EinsatzExportPacket packet,
            List<Team> selectedTeams,
            HashSet<string> teamIdSet,
            EinsatzData einsatz)
        {
            var collectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Live-Tracks direkt aus den Teams
            foreach (var team in selectedTeams)
            {
                foreach (var snap in team.TrackSnapshots ?? new())
                {
                    if (collectedIds.Add(snap.Id))
                        packet.TrackSnapshots.Add(snap);
                }
            }

            // Archivierte Snapshots aus EinsatzData (nach Team-Stopp gespeichert)
            foreach (var snap in einsatz.TrackSnapshots ?? new())
            {
                if (collectedIds.Contains(snap.Id)) continue;

                if ((!string.IsNullOrEmpty(snap.TeamId) && teamIdSet.Contains(snap.TeamId)) ||
                    selectedTeams.Any(t => string.Equals(t.TeamName, snap.TeamName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (collectedIds.Add(snap.Id))
                        packet.TrackSnapshots.Add(snap);
                }
            }
        }

        private const string FallbackLabelName = "unbekannt";
        private const string FallbackExportName = "export";

        private static string MakeSafeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return FallbackLabelName;

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
                sb.Append(Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '_' : c);

            var result = sb.ToString().Trim('_');
            return result.Length == 0 ? FallbackExportName :
                   result.Length > 50 ? result[..50] : result;
        }
    }
}

