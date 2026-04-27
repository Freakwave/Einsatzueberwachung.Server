// EinsatzMergeService — Kern-Implementierung der Einsatz-Zusammenführung
// Enthält: Vorschlags-Engine, atomare Apply-Logik, Revert-Mechanismus

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Models.Merge;

namespace Einsatzueberwachung.Domain.Services
{
    /// <summary>
    /// Implementiert den vollständigen Merge-Workflow für Einsatz-Exporte.
    /// </summary>
    public class EinsatzMergeService : IEinsatzMergeService
    {
        private readonly IEinsatzService _einsatzService;
        private readonly IMasterDataService _masterDataService;
        private readonly IArchivService _archivService;
        private readonly ITimeService? _timeService;

        /// <summary>Suffix für umbenannte Suchgebiete bei Namenskonflikt.</summary>
        private const string SearchAreaRenameSuffix = "_importiert";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private DateTime Now => _timeService?.Now ?? DateTime.Now;

        public EinsatzMergeService(
            IEinsatzService einsatzService,
            IMasterDataService masterDataService,
            IArchivService archivService,
            ITimeService? timeService = null)
        {
            _einsatzService = einsatzService;
            _masterDataService = masterDataService;
            _archivService = archivService;
            _timeService = timeService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ParseExportPacket
        // ─────────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────
        // CreateSessionAsync
        // ─────────────────────────────────────────────────────────────────────

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

            // Stammdaten laden
            var localPersonal = await _masterDataService.GetPersonalListAsync();
            var localDogs = await _masterDataService.GetDogListAsync();
            var localDrones = await _masterDataService.GetDroneListAsync();

            // Stammdaten-Items mit Vorschlägen befüllen
            session.PersonalItems = packet.Personal
                .Select(p => BuildPersonalMergeItem(p, localPersonal))
                .ToList();

            session.DogItems = packet.Dogs
                .Select(d => BuildDogMergeItem(d, localDogs, packet.Personal, localPersonal))
                .ToList();

            session.DroneItems = packet.Drones
                .Select(d => BuildDroneMergeItem(d, localDrones))
                .ToList();

            // Operative Daten gegen lokale Zieldaten prüfen
            var (localTeams, localNotes, localAreas, localMarkers, localTracks) =
                await GetLocalOperativeDataAsync(targetArchivedEinsatzId);

            // Teams
            session.TeamItems = packet.Teams.Select(t => new TeamMergeItem
            {
                ImportedTeam = t,
                HasLocalConflict = localTeams.Any(lt => lt.TeamId == t.TeamId),
                LocalConflictTeam = localTeams.FirstOrDefault(lt => lt.TeamId == t.TeamId),
                ShouldImport = true,
                ResolvedMemberNames = GetMemberNames(t)
            }).ToList();

            // Notizen
            var localNoteIds = new HashSet<string>(localNotes.Select(n => n.Id));
            session.NoteItems = packet.Notes.Select(n => new NoteMergeItem
            {
                Note = n,
                IsAlreadyPresent = localNoteIds.Contains(n.Id),
                ShouldImport = !localNoteIds.Contains(n.Id)
            })
            .OrderBy(n => n.Note.Timestamp)
            .ToList();

            // Suchgebiete
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

            // Statistiken für additive Elemente (Tracks, Marker)
            var localMarkerIds = new HashSet<string>(localMarkers.Select(m => m.Id));
            var localTrackIds = new HashSet<string>(localTracks.Select(t => t.Id));
            session.NewMarkerCount = packet.MapMarkers.Count(m => !localMarkerIds.Contains(m.Id));
            session.NewTrackSnapshotCount = packet.TrackSnapshots.Count(t => !localTrackIds.Contains(t.Id));

            return session;
        }

        // ─────────────────────────────────────────────────────────────────────
        // RebuildIdRemapping
        // ─────────────────────────────────────────────────────────────────────

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
                        // Die neue ID wird erst beim Apply vergeben; während des Wizards
                        // verwenden wir eine Platzhalter-Markierung mit dem Präfix "new:"
                        if (!session.IdRemapping.ContainsKey(item.ImportedId))
                            session.IdRemapping[item.ImportedId] = "new:" + item.ImportedId;
                        break;

                    case MergeDecision.Skip:
                        session.IdRemapping[item.ImportedId] = string.Empty; // leerer Wert = weglassen
                        break;
                }
            }

            // Member-Namen in TeamItems neu auflösen
            foreach (var teamItem in session.TeamItems)
            {
                teamItem.ResolvedMemberNames = GetResolvedMemberNames(teamItem.ImportedTeam, session.IdRemapping);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ApplyMergeAsync
        // ─────────────────────────────────────────────────────────────────────

        public async Task<MergeHistoryEntry> ApplyMergeAsync(EinsatzMergeSession session)
        {
            if (!session.AllMasterDataResolved)
                throw new InvalidOperationException("Nicht alle Stammdaten-Einträge wurden zugeordnet.");

            var historyEntry = new MergeHistoryEntry
            {
                MergedAt = Now,
                Label = session.Packet.Label
            };

            // ── 1. Stammdaten: neu erstellte Einträge anlegen und Remapping finalisieren ──
            var finalRemapping = new Dictionary<string, string>(session.IdRemapping);

            foreach (var item in session.PersonalItems.Where(i => i.Decision == MergeDecision.CreateNew))
            {
                var entry = (PersonalEntry)item.ImportedEntry;
                var newEntry = ClonePersonal(entry);
                await _masterDataService.AddPersonalAsync(newEntry);
                finalRemapping[item.ImportedId] = newEntry.Id;
                historyEntry.CreatedPersonalIds.Add(newEntry.Id);
                historyEntry.PersonalCreated++;
            }

            foreach (var item in session.DogItems.Where(i => i.Decision == MergeDecision.CreateNew))
            {
                var entry = (DogEntry)item.ImportedEntry;
                var newEntry = CloneDog(entry);
                await _masterDataService.AddDogAsync(newEntry);
                finalRemapping[item.ImportedId] = newEntry.Id;
                historyEntry.CreatedDogIds.Add(newEntry.Id);
                historyEntry.DogsCreated++;
            }

            foreach (var item in session.DroneItems.Where(i => i.Decision == MergeDecision.CreateNew))
            {
                var entry = (DroneEntry)item.ImportedEntry;
                var newEntry = CloneDrone(entry);
                await _masterDataService.AddDroneAsync(newEntry);
                finalRemapping[item.ImportedId] = newEntry.Id;
                historyEntry.CreatedDroneIds.Add(newEntry.Id);
                historyEntry.DronesCreated++;
            }

            // Felder überschreiben wenn gewünscht (LinkToExisting + OverwriteLocalFields)
            await ApplyFieldOverwritesAsync(session.PersonalItems, finalRemapping);
            await ApplyDogFieldOverwritesAsync(session.DogItems, finalRemapping);
            await ApplyDroneFieldOverwritesAsync(session.DroneItems, finalRemapping);

            // ── 2. Operative Daten anwenden ──
            if (session.TargetArchivedEinsatzId == null)
            {
                await ApplyToActiveEinsatzAsync(session, finalRemapping, historyEntry);
            }
            else
            {
                await ApplyToArchivedEinsatzAsync(session, finalRemapping, historyEntry);
            }

            // ── 3. Protokolleintrag speichern ──
            historyEntry.TeamsAdded = historyEntry.AddedTeamIds.Count;
            historyEntry.NotesAdded = historyEntry.AddedNoteIds.Count;
            historyEntry.SearchAreasAdded = historyEntry.AddedSearchAreaIds.Count;
            historyEntry.MarkersAdded = historyEntry.AddedMapMarkerIds.Count;
            historyEntry.TracksAdded = historyEntry.AddedTrackSnapshotIds.Count;

            await PersistMergeHistoryEntryAsync(historyEntry, session.TargetArchivedEinsatzId);

            // Zusammenführungs-Systemnotiz schreiben
            var label = string.IsNullOrWhiteSpace(session.Packet.Label)
                ? "Import"
                : $"Import '{session.Packet.Label}'";
            var summaryText = $"{label} zusammengeführt am " +
                              $"{historyEntry.MergedAt:dd.MM.yyyy HH:mm} — " +
                              $"{historyEntry.TeamsAdded} Teams, {historyEntry.NotesAdded} Notizen, " +
                              $"{historyEntry.SearchAreasAdded} Gebiete integriert.";

            if (session.TargetArchivedEinsatzId == null)
            {
                await _einsatzService.AddGlobalNoteAsync(summaryText, GlobalNotesEntryType.System);
            }

            session.AppliedMerge = historyEntry;
            session.CurrentStep = MergeWizardStep.Result;

            return historyEntry;
        }

        // ─────────────────────────────────────────────────────────────────────
        // RevertMergeAsync
        // ─────────────────────────────────────────────────────────────────────

        public async Task RevertMergeAsync(string mergeId, string? archivedEinsatzId = null)
        {
            var history = await GetMergeHistoryAsync(archivedEinsatzId);
            var entry = history.FirstOrDefault(e => e.MergeId == mergeId);
            if (entry == null || entry.IsReverted)
                return;

            if (archivedEinsatzId == null)
            {
                // Aktiver Einsatz
                foreach (var id in entry.AddedTeamIds)
                    await _einsatzService.RemoveTeamAsync(id);

                foreach (var id in entry.AddedNoteIds)
                    await _einsatzService.RemoveGlobalNoteAsync(id);

                foreach (var id in entry.AddedSearchAreaIds)
                    await _einsatzService.DeleteSearchAreaAsync(id);

                foreach (var id in entry.AddedMapMarkerIds)
                    await _einsatzService.RemoveMapMarkerAsync(id);

                // GPS-Tracks aus EinsatzData entfernen
                var einsatzData = _einsatzService.CurrentEinsatz;
                var trackSet = new HashSet<string>(entry.AddedTrackSnapshotIds);
                einsatzData.TrackSnapshots.RemoveAll(t => trackSet.Contains(t.Id));

                // Frisch angelegte Stammdaten entfernen
                foreach (var id in entry.CreatedPersonalIds)
                    await _masterDataService.DeletePersonalAsync(id);
                foreach (var id in entry.CreatedDogIds)
                    await _masterDataService.DeleteDogAsync(id);
                foreach (var id in entry.CreatedDroneIds)
                    await _masterDataService.DeleteDroneAsync(id);

                // Protokolleintrag als rückgängig markieren
                entry.IsReverted = true;
                entry.RevertedAt = Now;

                // Systemnotiz über den Revert
                var revertLabel = string.IsNullOrWhiteSpace(entry.Label)
                    ? "Import"
                    : $"Import '{entry.Label}'";
                await _einsatzService.AddGlobalNoteAsync(
                    $"{revertLabel} vom {entry.FormattedMergedAt} rückgängig gemacht.",
                    GlobalNotesEntryType.System);
            }
            else
            {
                var archived = await _archivService.GetByIdAsync(archivedEinsatzId);
                if (archived == null) return;

                var teamSet = new HashSet<string>(entry.AddedTeamIds);
                var noteSet = new HashSet<string>(entry.AddedNoteIds);
                var areaSet = new HashSet<string>(entry.AddedSearchAreaIds);
                var markerSet = new HashSet<string>(entry.AddedMapMarkerIds);
                var trackSet = new HashSet<string>(entry.AddedTrackSnapshotIds);

                archived.Teams.RemoveAll(t => teamSet.Contains(t.TeamId));
                archived.GlobalNotesEntries.RemoveAll(n => noteSet.Contains(n.Id));
                archived.SearchAreas.RemoveAll(a => areaSet.Contains(a.Id));
                archived.TrackSnapshots.RemoveAll(t => trackSet.Contains(t.Id));

                // Frisch angelegte Stammdaten
                foreach (var id in entry.CreatedPersonalIds)
                    await _masterDataService.DeletePersonalAsync(id);
                foreach (var id in entry.CreatedDogIds)
                    await _masterDataService.DeleteDogAsync(id);
                foreach (var id in entry.CreatedDroneIds)
                    await _masterDataService.DeleteDroneAsync(id);

                entry.IsReverted = true;
                entry.RevertedAt = Now;

                await _archivService.UpdateArchivedEinsatzAsync(archived);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GetMergeHistoryAsync
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<MergeHistoryEntry>> GetMergeHistoryAsync(string? archivedEinsatzId = null)
        {
            if (archivedEinsatzId == null)
            {
                return _einsatzService.CurrentEinsatz.MergeHistory ?? new();
            }

            var archived = await _archivService.GetByIdAsync(archivedEinsatzId);
            return archived?.MergeHistory ?? new();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers — suggestion engine
        // ─────────────────────────────────────────────────────────────────────

        private static MasterDataMergeItem BuildPersonalMergeItem(PersonalEntry imported, List<PersonalEntry> local)
        {
            var item = new MasterDataMergeItem
            {
                ImportedId = imported.Id,
                DisplayName = imported.FullName,
                DetailsDisplay = imported.SkillsShortDisplay,
                EntityType = MergeEntityType.Personal,
                ImportedEntry = imported
            };

            item.Suggestions = local
                .Select(p => ScorePersonal(imported, p))
                .Where(c => c.ConfidenceScore > 0)
                .OrderByDescending(c => c.ConfidenceScore)
                .Take(5)
                .ToList();

            // Auto-Vorauswahl: bester Kandidat, bei dem Vorname UND Nachname exakt übereinstimmen
            var localById = local.ToDictionary(p => p.Id);
            var bestNameMatch = item.Suggestions
                .Select(c => localById.TryGetValue(c.LocalId, out var lp) ? lp : null)
                .FirstOrDefault(p => p != null &&
                    string.Equals(imported.Vorname, p.Vorname, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(imported.Nachname, p.Nachname, StringComparison.OrdinalIgnoreCase));

            if (bestNameMatch != null)
            {
                item.Decision = MergeDecision.LinkToExisting;
                item.SelectedLocalId = bestNameMatch.Id;
            }

            return item;
        }

        private static MasterDataMergeItem BuildDogMergeItem(
            DogEntry imported,
            List<DogEntry> local,
            List<PersonalEntry> importedPersonal,
            List<PersonalEntry> localPersonal)
        {
            var item = new MasterDataMergeItem
            {
                ImportedId = imported.Id,
                DisplayName = imported.Name,
                DetailsDisplay = imported.SpecializationsShortDisplay,
                EntityType = MergeEntityType.Dog,
                ImportedEntry = imported
            };

            item.Suggestions = local
                .Select(d => ScoreDog(imported, d))
                .Where(c => c.ConfidenceScore > 0)
                .OrderByDescending(c => c.ConfidenceScore)
                .Take(5)
                .ToList();

            // Auto-Vorauswahl: bester Kandidat, bei dem Hundename UND
            // mind. ein Hundeführer-Name übereinstimmen
            var importedHandlerNames = ResolveHandlerNames(imported.HundefuehrerIds, importedPersonal);
            if (importedHandlerNames.Count > 0)
            {
                var localById = local.ToDictionary(d => d.Id);
                var bestMatch = item.Suggestions
                    .Select(c => localById.TryGetValue(c.LocalId, out var ld) ? ld : null)
                    .FirstOrDefault(d => d != null &&
                        string.Equals(imported.Name, d.Name, StringComparison.OrdinalIgnoreCase) &&
                        ResolveHandlerNames(d.HundefuehrerIds, localPersonal)
                            .Overlaps(importedHandlerNames));

                if (bestMatch != null)
                {
                    item.Decision = MergeDecision.LinkToExisting;
                    item.SelectedLocalId = bestMatch.Id;
                }
            }

            // Fallback: Wenn kein Hundeführer bekannt ist, aber der beste Treffer einen Score
            // von 100 % hat (gleiche ID + gleicher Name), trotzdem vorauswählen.
            if (item.Decision == MergeDecision.Undecided && item.Suggestions.Count > 0)
            {
                var best = item.Suggestions[0];
                if (best.ConfidenceScore >= 1.0 &&
                    string.Equals(imported.Name, best.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    item.Decision = MergeDecision.LinkToExisting;
                    item.SelectedLocalId = best.LocalId;
                }
            }

            return item;
        }

        /// <summary>
        /// Löst eine Liste von Hundeführer-IDs in eine Menge von Vollnamen auf.
        /// Unbekannte IDs werden ignoriert.
        /// </summary>
        private static HashSet<string> ResolveHandlerNames(
            IEnumerable<string> handlerIds,
            List<PersonalEntry> personal)
        {
            var byId = personal.ToDictionary(p => p.Id);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in handlerIds)
            {
                if (byId.TryGetValue(id, out var p))
                    names.Add(p.FullName);
            }
            return names;
        }

        private static MasterDataMergeItem BuildDroneMergeItem(DroneEntry imported, List<DroneEntry> local)
        {
            var item = new MasterDataMergeItem
            {
                ImportedId = imported.Id,
                DisplayName = imported.DisplayName,
                DetailsDisplay = imported.FullDescription,
                EntityType = MergeEntityType.Drone,
                ImportedEntry = imported
            };

            item.Suggestions = local
                .Select(d => ScoreDrone(imported, d))
                .Where(c => c.ConfidenceScore > 0)
                .OrderByDescending(c => c.ConfidenceScore)
                .Take(5)
                .ToList();

            return item;
        }

        private static MasterDataMergeCandidate ScorePersonal(PersonalEntry imported, PersonalEntry local)
        {
            double score = 0;
            string reason = "PARTIAL";
            string label = "Ähnlicher Name";

            var importedName = imported.FullName.ToLowerInvariant();
            var localName = local.FullName.ToLowerInvariant();
            bool exactName = importedName == localName;
            bool partialName = !exactName && ContainsPartialName(importedName, localName);

            if (imported.Id == local.Id)
            {
                // ID-Treffer ist nur ein starkes Signal wenn auch der Name übereinstimmt.
                // Ohne Namensübereinstimmung könnte es eine zufällige ID-Kollision sein.
                score += (exactName || partialName) ? 0.85 : 0.15;
                reason = "SAME_ID";
                label = "Gleiche ID";
            }

            if (exactName)
            {
                score += 0.70;
                if (reason != "SAME_ID") { reason = "EXACT_NAME"; label = "Gleicher Name"; }
            }
            else if (partialName)
            {
                score += 0.35;
                if (reason == "PARTIAL") { reason = "PARTIAL_NAME"; label = "Ähnlicher Name"; }
            }

            if (imported.Skills != PersonalSkills.None && imported.Skills == local.Skills)
            {
                score += 0.05;
            }

            if (score <= 0) return new MasterDataMergeCandidate { ConfidenceScore = 0 };

            return new MasterDataMergeCandidate
            {
                LocalId = local.Id,
                DisplayName = local.FullName,
                MatchReason = reason,
                MatchReasonLabel = label,
                ConfidenceScore = Math.Min(score, 1.0),
                DetailsDisplay = local.SkillsShortDisplay
            };
        }

        private static MasterDataMergeCandidate ScoreDog(DogEntry imported, DogEntry local)
        {
            double score = 0;
            string reason = "PARTIAL";
            string label = "Ähnlicher Name";

            var importedName = imported.Name.ToLowerInvariant();
            var localName = local.Name.ToLowerInvariant();
            bool exactName = importedName == localName;
            bool partialName = !exactName && ContainsPartialName(importedName, localName);

            if (imported.Id == local.Id)
            {
                // ID-Treffer ist nur ein starkes Signal wenn auch der Name übereinstimmt.
                // Ohne Namensübereinstimmung könnte es eine zufällige ID-Kollision sein.
                score += (exactName || partialName) ? 0.85 : 0.15;
                reason = "SAME_ID";
                label = "Gleiche ID";
            }

            if (exactName)
            {
                score += 0.70;
                if (reason != "SAME_ID") { reason = "EXACT_NAME"; label = "Gleicher Name"; }
            }
            else if (partialName)
            {
                score += 0.35;
                if (reason == "PARTIAL") { reason = "PARTIAL_NAME"; label = "Ähnlicher Name"; }
            }

            if (imported.Specializations != Models.Enums.DogSpecialization.None &&
                imported.Specializations == local.Specializations)
            {
                score += 0.05;
            }

            if (score <= 0) return new MasterDataMergeCandidate { ConfidenceScore = 0 };

            return new MasterDataMergeCandidate
            {
                LocalId = local.Id,
                DisplayName = local.Name,
                MatchReason = reason,
                MatchReasonLabel = label,
                ConfidenceScore = Math.Min(score, 1.0),
                DetailsDisplay = local.SpecializationsShortDisplay
            };
        }

        private static MasterDataMergeCandidate ScoreDrone(DroneEntry imported, DroneEntry local)
        {
            double score = 0;
            string reason = "PARTIAL";
            string label = "Ähnlicher Name";

            var importedName = imported.DisplayName.ToLowerInvariant();
            var localName = local.DisplayName.ToLowerInvariant();
            bool exactName = importedName == localName;
            bool partialName = !exactName && ContainsPartialName(importedName, localName);

            if (imported.Id == local.Id)
            {
                // ID-Treffer ist nur ein starkes Signal wenn auch der Name übereinstimmt.
                // Ohne Namensübereinstimmung könnte es eine zufällige ID-Kollision sein.
                score += (exactName || partialName) ? 0.85 : 0.15;
                reason = "SAME_ID";
                label = "Gleiche ID";
            }

            if (exactName)
            {
                score += 0.70;
                if (reason != "SAME_ID") { reason = "EXACT_NAME"; label = "Gleicher Name"; }
            }
            else if (partialName)
            {
                score += 0.35;
                if (reason == "PARTIAL") { reason = "PARTIAL_NAME"; label = "Ähnlicher Name"; }
            }

            if (imported.Seriennummer == local.Seriennummer &&
                !string.IsNullOrEmpty(imported.Seriennummer))
            {
                score += 0.10;
                if (reason == "PARTIAL") { reason = "SERIAL"; label = "Gleiche Seriennummer"; }
            }

            if (score <= 0) return new MasterDataMergeCandidate { ConfidenceScore = 0 };

            return new MasterDataMergeCandidate
            {
                LocalId = local.Id,
                DisplayName = local.DisplayName,
                MatchReason = reason,
                MatchReasonLabel = label,
                ConfidenceScore = Math.Min(score, 1.0),
                DetailsDisplay = local.FullDescription
            };
        }

        private static bool ContainsPartialName(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
                   b.Contains(a, StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers — apply
        // ─────────────────────────────────────────────────────────────────────

        private async Task ApplyToActiveEinsatzAsync(
            EinsatzMergeSession session,
            Dictionary<string, string> remapping,
            MergeHistoryEntry historyEntry)
        {
            var einsatz = _einsatzService.CurrentEinsatz;
            var localTrackIds = new HashSet<string>(einsatz.TrackSnapshots.Select(t => t.Id));
            var localMarkerIds = new HashSet<string>((await _einsatzService.GetMapMarkersAsync()).Select(m => m.Id));

            // Teams
            foreach (var teamItem in session.TeamItems.Where(t => t.ShouldImport))
            {
                var team = ApplyRemappingToTeam(teamItem.ImportedTeam, remapping);

                if (teamItem.HasLocalConflict)
                {
                    // Neues Team mit neuer ID (Konflikt gelöst durch Duplizierung)
                    team.TeamId = Guid.NewGuid().ToString();
                }

                await _einsatzService.AddTeamAsync(team);
                historyEntry.AddedTeamIds.Add(team.TeamId);
            }

            // Notizen
            foreach (var noteItem in session.NoteItems.Where(n => !n.IsAlreadyPresent && n.ShouldImport))
            {
                var note = noteItem.Note;
                _einsatzService.CurrentEinsatz.GlobalNotesEntries.Add(note);
                _einsatzService.GlobalNotes.Add(note);
                historyEntry.AddedNoteIds.Add(note.Id);
            }

            // Suchgebiete
            foreach (var areaItem in session.SearchAreaItems.Where(
                a => a.ShouldImport && a.ConflictType != SearchAreaConflict.SameIdExists))
            {
                var area = areaItem.ImportedArea;

                // ID-Remapping für AssignedTeamId anwenden
                if (!string.IsNullOrEmpty(area.AssignedTeamId) &&
                    remapping.TryGetValue(area.AssignedTeamId, out var mappedTeamId))
                {
                    area.AssignedTeamId = mappedTeamId;
                }

                if (areaItem.ConflictType == SearchAreaConflict.SameNameDifferentId)
                {
                    switch (areaItem.NameConflictResolution)
                    {
                        case SearchAreaNameConflictResolution.Rename:
                            area.Name = area.Name + SearchAreaRenameSuffix;
                            break;

                        case SearchAreaNameConflictResolution.ReplaceLocal:
                            await _einsatzService.DeleteSearchAreaAsync(areaItem.LocalConflictArea!.Id);
                            break;

                        case SearchAreaNameConflictResolution.KeepBoth:
                            area.Id = Guid.NewGuid().ToString();
                            break;
                    }
                }

                await _einsatzService.AddSearchAreaAsync(area);
                historyEntry.AddedSearchAreaIds.Add(area.Id);
            }

            // Marker (rein additiv)
            foreach (var marker in session.Packet.MapMarkers.Where(m => !localMarkerIds.Contains(m.Id)))
            {
                await _einsatzService.AddMapMarkerAsync(marker);
                historyEntry.AddedMapMarkerIds.Add(marker.Id);
            }

            // GPS-Tracks (rein additiv)
            foreach (var track in session.Packet.TrackSnapshots.Where(t => !localTrackIds.Contains(t.Id)))
            {
                einsatz.TrackSnapshots.Add(track);
                historyEntry.AddedTrackSnapshotIds.Add(track.Id);
            }
        }

        private async Task ApplyToArchivedEinsatzAsync(
            EinsatzMergeSession session,
            Dictionary<string, string> remapping,
            MergeHistoryEntry historyEntry)
        {
            var archived = await _archivService.GetByIdAsync(session.TargetArchivedEinsatzId!);
            if (archived == null) throw new InvalidOperationException("Archivierter Einsatz nicht gefunden.");

            var localTrackIds = new HashSet<string>(archived.TrackSnapshots.Select(t => t.Id));
            var localNoteIds = new HashSet<string>(archived.GlobalNotesEntries.Select(n => n.Id));
            var localAreaIds = new HashSet<string>(archived.SearchAreas.Select(a => a.Id));

            // Teams als ArchivedTeam hinzufügen
            foreach (var teamItem in session.TeamItems.Where(t => t.ShouldImport))
            {
                var archivedTeam = ArchivedTeam.FromTeam(teamItem.ImportedTeam);
                archivedTeam.MemberNames = teamItem.ResolvedMemberNames.ToList();
                archived.Teams.Add(archivedTeam);
                historyEntry.AddedTeamIds.Add(teamItem.ImportedTeam.TeamId);
            }

            // Notizen
            foreach (var noteItem in session.NoteItems.Where(n => !n.IsAlreadyPresent && n.ShouldImport))
            {
                archived.GlobalNotesEntries.Add(noteItem.Note);
                historyEntry.AddedNoteIds.Add(noteItem.Note.Id);
            }

            // Suchgebiete
            foreach (var areaItem in session.SearchAreaItems.Where(
                a => a.ShouldImport && a.ConflictType != SearchAreaConflict.SameIdExists))
            {
                var area = areaItem.ImportedArea;

                if (!string.IsNullOrEmpty(area.AssignedTeamId) &&
                    remapping.TryGetValue(area.AssignedTeamId, out var mappedId))
                {
                    area.AssignedTeamId = mappedId;
                }

                if (areaItem.ConflictType == SearchAreaConflict.SameNameDifferentId)
                {
                    switch (areaItem.NameConflictResolution)
                    {
                        case SearchAreaNameConflictResolution.Rename:
                            area.Name = area.Name + SearchAreaRenameSuffix;
                            break;
                        case SearchAreaNameConflictResolution.ReplaceLocal:
                            archived.SearchAreas.RemoveAll(a => a.Id == areaItem.LocalConflictArea!.Id);
                            break;
                        case SearchAreaNameConflictResolution.KeepBoth:
                            area.Id = Guid.NewGuid().ToString();
                            break;
                    }
                }

                archived.SearchAreas.Add(area);
                historyEntry.AddedSearchAreaIds.Add(area.Id);
            }

            // GPS-Tracks
            foreach (var track in session.Packet.TrackSnapshots.Where(t => !localTrackIds.Contains(t.Id)))
            {
                archived.TrackSnapshots.Add(track);
                historyEntry.AddedTrackSnapshotIds.Add(track.Id);
            }

            archived.LastMergedAt = Now;

            await _archivService.UpdateArchivedEinsatzAsync(archived);
        }

        private async Task PersistMergeHistoryEntryAsync(
            MergeHistoryEntry entry, string? archivedEinsatzId)
        {
            if (archivedEinsatzId == null)
            {
                _einsatzService.CurrentEinsatz.MergeHistory ??= new();
                _einsatzService.CurrentEinsatz.MergeHistory.Insert(0, entry);
            }
            else
            {
                var archived = await _archivService.GetByIdAsync(archivedEinsatzId);
                if (archived == null) return;

                archived.MergeHistory ??= new();
                archived.MergeHistory.Insert(0, entry);
                archived.LastMergedAt = entry.MergedAt;

                await _archivService.UpdateArchivedEinsatzAsync(archived);
            }
        }

        private async Task ApplyFieldOverwritesAsync(
            List<MasterDataMergeItem> items, Dictionary<string, string> remapping)
        {
            foreach (var item in items.Where(x =>
                x.Decision == MergeDecision.LinkToExisting &&
                x.OverwriteLocalFields &&
                !string.IsNullOrEmpty(x.SelectedLocalId)))
            {
                var local = await _masterDataService.GetPersonalByIdAsync(item.SelectedLocalId!);
                if (local == null) continue;

                var imported = (PersonalEntry)item.ImportedEntry;
                local.Vorname = imported.Vorname;
                local.Nachname = imported.Nachname;
                local.Skills = imported.Skills;
                if (!string.IsNullOrWhiteSpace(imported.Notizen))
                    local.Notizen = imported.Notizen;

                await _masterDataService.UpdatePersonalAsync(local);
            }
        }

        private async Task ApplyDogFieldOverwritesAsync(
            List<MasterDataMergeItem> items, Dictionary<string, string> remapping)
        {
            foreach (var item in items.Where(x =>
                x.Decision == MergeDecision.LinkToExisting &&
                x.OverwriteLocalFields &&
                !string.IsNullOrEmpty(x.SelectedLocalId)))
            {
                var local = await _masterDataService.GetDogByIdAsync(item.SelectedLocalId!);
                if (local == null) continue;

                var imported = (DogEntry)item.ImportedEntry;
                local.Name = imported.Name;
                local.Rasse = imported.Rasse;
                local.Specializations = imported.Specializations;
                if (!string.IsNullOrWhiteSpace(imported.Notizen))
                    local.Notizen = imported.Notizen;

                await _masterDataService.UpdateDogAsync(local);
            }
        }

        private async Task ApplyDroneFieldOverwritesAsync(
            List<MasterDataMergeItem> items, Dictionary<string, string> remapping)
        {
            foreach (var item in items.Where(x =>
                x.Decision == MergeDecision.LinkToExisting &&
                x.OverwriteLocalFields &&
                !string.IsNullOrEmpty(x.SelectedLocalId)))
            {
                var local = await _masterDataService.GetDroneByIdAsync(item.SelectedLocalId!);
                if (local == null) continue;

                var imported = (DroneEntry)item.ImportedEntry;
                local.Name = imported.Name;
                local.Modell = imported.Modell;
                local.Hersteller = imported.Hersteller;
                if (!string.IsNullOrWhiteSpace(imported.Notizen))
                    local.Notizen = imported.Notizen;

                await _masterDataService.UpdateDroneAsync(local);
            }
        }

        private static Team ApplyRemappingToTeam(Team team, Dictionary<string, string> remapping)
        {
            // Nicht das Original mutieren — flache Kopie
            var t = ShallowCopyTeam(team);

            if (!string.IsNullOrEmpty(t.HundefuehrerId) &&
                remapping.TryGetValue(t.HundefuehrerId, out var hfId))
                t.HundefuehrerId = hfId;

            if (!string.IsNullOrEmpty(t.HelferId) &&
                remapping.TryGetValue(t.HelferId, out var hId))
                t.HelferId = hId;

            if (!string.IsNullOrEmpty(t.DogId) &&
                remapping.TryGetValue(t.DogId, out var dId))
                t.DogId = dId;

            if (!string.IsNullOrEmpty(t.DroneId) &&
                remapping.TryGetValue(t.DroneId, out var drId))
                t.DroneId = drId;

            // Felder bei "Skip" leeren
            if (t.HundefuehrerId == string.Empty) { t.HundefuehrerName = string.Empty; }
            if (t.HelferId == string.Empty) { t.HelferName = string.Empty; }
            if (t.DogId == string.Empty) { t.DogName = string.Empty; }
            if (t.DroneId == string.Empty) { t.DroneType = string.Empty; }

            return t;
        }

        private static Team ShallowCopyTeam(Team src)
        {
            return new Team
            {
                TeamId = src.TeamId,
                TeamName = src.TeamName,
                DogName = src.DogName,
                DogId = src.DogId,
                DogSpecialization = src.DogSpecialization,
                HundefuehrerName = src.HundefuehrerName,
                HundefuehrerId = src.HundefuehrerId,
                HelferName = src.HelferName,
                HelferId = src.HelferId,
                SearchAreaName = src.SearchAreaName,
                SearchAreaId = src.SearchAreaId,
                ElapsedTime = src.ElapsedTime,
                IsRunning = false, // Importierte Teams werden nicht gestartet
                FirstWarningMinutes = src.FirstWarningMinutes,
                SecondWarningMinutes = src.SecondWarningMinutes,
                CreatedAt = src.CreatedAt,
                Notes = src.Notes,
                IsDroneTeam = src.IsDroneTeam,
                DroneType = src.DroneType,
                DroneId = src.DroneId,
                IsSupportTeam = src.IsSupportTeam,
                CollarId = src.CollarId,
                CollarName = src.CollarName
            };
        }

        private static List<string> GetMemberNames(Team team)
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(team.HundefuehrerName)) names.Add(team.HundefuehrerName);
            if (!string.IsNullOrEmpty(team.HelferName)) names.Add(team.HelferName);
            return names;
        }

        private static List<string> GetResolvedMemberNames(Team team, Dictionary<string, string> remapping)
        {
            var names = new List<string>();
            if (!string.IsNullOrEmpty(team.HundefuehrerName)) names.Add(team.HundefuehrerName);
            if (!string.IsNullOrEmpty(team.HelferName)) names.Add(team.HelferName);
            return names;
        }

        private async Task<(List<Team> teams, List<GlobalNotesEntry> notes, List<SearchArea> areas,
            List<MapMarker> markers, List<TeamTrackSnapshot> tracks)>
            GetLocalOperativeDataAsync(string? archivedEinsatzId)
        {
            if (archivedEinsatzId == null)
            {
                var einsatz = _einsatzService.CurrentEinsatz;
                return (
                    _einsatzService.Teams,
                    _einsatzService.GlobalNotes,
                    einsatz.SearchAreas,
                    await _einsatzService.GetMapMarkersAsync(),
                    einsatz.TrackSnapshots
                );
            }

            var archived = await _archivService.GetByIdAsync(archivedEinsatzId);
            if (archived == null)
                return (new(), new(), new(), new(), new());

            // ArchivedTeam → Team (nur TeamId/TeamName für Vergleiche)
            var teams = archived.Teams.Select(t => new Team
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName
            }).ToList();

            return (teams, archived.GlobalNotesEntries, archived.SearchAreas, new(), archived.TrackSnapshots);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Clone helpers
        // ─────────────────────────────────────────────────────────────────────

        private static PersonalEntry ClonePersonal(PersonalEntry src)
        {
            return new PersonalEntry
            {
                Id = Guid.NewGuid().ToString(),
                Vorname = src.Vorname,
                Nachname = src.Nachname,
                Skills = src.Skills,
                Notizen = src.Notizen,
                IsActive = src.IsActive,
                DiveraUserId = src.DiveraUserId
            };
        }

        private static DogEntry CloneDog(DogEntry src)
        {
            return new DogEntry
            {
                Id = Guid.NewGuid().ToString(),
                Name = src.Name,
                Rasse = src.Rasse,
                Alter = src.Alter,
                Specializations = src.Specializations,
                HundefuehrerIds = new List<string>(src.HundefuehrerIds),
                Notizen = src.Notizen,
                IsActive = src.IsActive
            };
        }

        private static DroneEntry CloneDrone(DroneEntry src)
        {
            return new DroneEntry
            {
                Id = Guid.NewGuid().ToString(),
                Name = src.Name,
                Modell = src.Modell,
                Hersteller = src.Hersteller,
                Seriennummer = src.Seriennummer,
                DrohnenpilotId = src.DrohnenpilotId,
                Notizen = src.Notizen,
                IsActive = src.IsActive
            };
        }
    }
}
