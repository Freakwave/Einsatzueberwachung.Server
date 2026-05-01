using System.Text.Json;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzMergeService
    {
        public EinsatzExportPacket? ParseExportPacket(byte[] json)
        {
            try
            {
                return JsonSerializer.Deserialize<EinsatzExportPacket>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public async Task<EinsatzMergeSession> CreateSessionAsync(
            EinsatzExportPacket packet,
            string? targetArchivedEinsatzId = null)
        {
            var session = new EinsatzMergeSession
            {
                Packet = packet,
                TargetArchivedEinsatzId = targetArchivedEinsatzId,
                CurrentStep = MergeWizardStep.Upload
            };

            var localPersonal = await _masterDataService.GetPersonalListAsync();
            var localDogs = await _masterDataService.GetDogListAsync();
            var localDrones = await _masterDataService.GetDroneListAsync();

            session.PersonalItems = packet.Personal
                .Select(p => BuildPersonalMergeItem(p, localPersonal))
                .ToList();

            session.DogItems = packet.Dogs
                .Select(d => BuildDogMergeItem(d, localDogs, packet.Personal, localPersonal))
                .ToList();

            session.DroneItems = packet.Drones
                .Select(d => BuildDroneMergeItem(d, localDrones))
                .ToList();

            var (localTeams, localNotes, localAreas, localMarkers, localTracks) =
                await GetLocalOperativeDataAsync(targetArchivedEinsatzId);

            session.TeamItems = packet.Teams.Select(t => new TeamMergeItem
            {
                ImportedTeam = t,
                HasLocalConflict = localTeams.Any(lt => lt.TeamId == t.TeamId),
                LocalConflictTeam = localTeams.FirstOrDefault(lt => lt.TeamId == t.TeamId),
                ShouldImport = true,
                ResolvedMemberNames = GetMemberNames(t)
            }).ToList();

            var localNoteIds = new HashSet<string>(localNotes.Select(n => n.Id));
            session.NoteItems = packet.Notes.Select(n => new NoteMergeItem
            {
                Note = n,
                IsAlreadyPresent = localNoteIds.Contains(n.Id),
                ShouldImport = !localNoteIds.Contains(n.Id)
            })
            .OrderBy(n => n.Note.Timestamp)
            .ToList();

            var localAreaIds = new HashSet<string>(localAreas.Select(a => a.Id));
            var localAreaNames = localAreas.ToDictionary(
                a => a.Name.ToLowerInvariant(), a => a, StringComparer.OrdinalIgnoreCase);

            session.SearchAreaItems = packet.SearchAreas.Select(a =>
            {
                if (localAreaIds.Contains(a.Id))
                {
                    return new SearchAreaMergeItem
                    {
                        ImportedArea = a,
                        ConflictType = SearchAreaConflict.SameIdExists,
                        ShouldImport = false
                    };
                }

                if (localAreaNames.TryGetValue(a.Name, out var conflict))
                {
                    return new SearchAreaMergeItem
                    {
                        ImportedArea = a,
                        ConflictType = SearchAreaConflict.SameNameDifferentId,
                        LocalConflictArea = conflict,
                        NameConflictResolution = SearchAreaNameConflictResolution.KeepBoth,
                        ShouldImport = true
                    };
                }

                return new SearchAreaMergeItem
                {
                    ImportedArea = a,
                    ConflictType = SearchAreaConflict.None,
                    ShouldImport = true
                };
            }).ToList();

            var localMarkerIds = new HashSet<string>(localMarkers.Select(m => m.Id));
            var localTrackIds = new HashSet<string>(localTracks.Select(t => t.Id));
            session.NewMarkerCount = packet.MapMarkers.Count(m => !localMarkerIds.Contains(m.Id));
            session.NewTrackSnapshotCount = packet.TrackSnapshots.Count(t => !localTrackIds.Contains(t.Id));

            return session;
        }

        public void RebuildIdRemapping(EinsatzMergeSession session)
        {
            session.IdRemapping.Clear();

            foreach (var item in session.AllMasterDataItems)
            {
                switch (item.Decision)
                {
                    case MergeDecision.LinkToExisting when !string.IsNullOrEmpty(item.SelectedLocalId):
                        session.IdRemapping[item.ImportedId] = item.SelectedLocalId;
                        break;

                    case MergeDecision.CreateNew:
                        if (!session.IdRemapping.ContainsKey(item.ImportedId))
                            session.IdRemapping[item.ImportedId] = "new:" + item.ImportedId;
                        break;

                    case MergeDecision.Skip:
                        session.IdRemapping[item.ImportedId] = string.Empty;
                        break;
                }
            }

            foreach (var teamItem in session.TeamItems)
            {
                teamItem.ResolvedMemberNames = GetResolvedMemberNames(teamItem.ImportedTeam, session.IdRemapping);
            }
        }

        public async Task<List<MergeHistoryEntry>> GetMergeHistoryAsync(string? archivedEinsatzId = null)
        {
            if (archivedEinsatzId == null)
            {
                return _einsatzService.CurrentEinsatz.MergeHistory ?? new();
            }

            var archived = await _archivService.GetByIdAsync(archivedEinsatzId);
            return archived?.MergeHistory ?? new();
        }
    }
}
