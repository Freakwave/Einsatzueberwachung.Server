using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzExportService
    {
        private async Task CollectStammdatenAsync(EinsatzExportPacket packet, List<Team> teams)
        {
            var personalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var droneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var team in teams)
            {
                if (!string.IsNullOrEmpty(team.HundefuehrerId)) personalIds.Add(team.HundefuehrerId);
                foreach (var hid in team.HelferIds)
                {
                    if (!string.IsNullOrEmpty(hid)) personalIds.Add(hid);
                }
                if (!string.IsNullOrEmpty(team.DogId))          dogIds.Add(team.DogId);
                if (!string.IsNullOrEmpty(team.DroneId))        droneIds.Add(team.DroneId);
            }

            if (personalIds.Any())
            {
                var all = await _masterDataService.GetPersonalListAsync();
                packet.Personal.AddRange(all.Where(p => personalIds.Contains(p.Id)));
            }

            if (dogIds.Any())
            {
                var allDogs = await _masterDataService.GetDogListAsync();
                var dogs = allDogs.Where(d => dogIds.Contains(d.Id)).ToList();
                packet.Dogs.AddRange(dogs);

                var existingPersonalIds = new HashSet<string>(
                    packet.Personal.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);

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

            foreach (var team in selectedTeams)
            {
                foreach (var snap in team.TrackSnapshots ?? new())
                {
                    if (collectedIds.Add(snap.Id))
                        packet.TrackSnapshots.Add(snap);
                }
            }

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
    }
}
