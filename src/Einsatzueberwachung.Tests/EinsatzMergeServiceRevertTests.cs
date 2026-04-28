// EinsatzMergeServiceRevertTests.cs
// Prüft, ob das Rückgängigmachen (Revert) einer Unterabschnitt-Integration
// den Originalzustand vollständig wiederherstellt — ohne Datenbankkorruption.
//
// Jeder Test folgt dem Schema:
//   Arrange  → Ausgangszustand aufbauen
//   Apply    → Unterabschnitt importieren (ApplyMergeAsync)
//   Assert*  → Zustand nach Import prüfen (optional)
//   Revert   → Import rückgängig machen (RevertMergeAsync)
//   Assert   → Originalzustand wiederhergestellt

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Models.Merge;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// In-Memory-Fakes (keine Datei-I/O, keine Persistenz)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>In-Memory-Stub für IMasterDataService – für Unit-Tests ohne Datei-I/O.</summary>
internal sealed class FakeMasterDataService : IMasterDataService
{
    public List<PersonalEntry> Personal { get; } = new();
    public List<DogEntry> Dogs { get; } = new();
    public List<DroneEntry> Drones { get; } = new();

    public Task<List<PersonalEntry>> GetPersonalListAsync() => Task.FromResult(Personal.ToList());
    public Task<PersonalEntry?> GetPersonalByIdAsync(string id) =>
        Task.FromResult(Personal.FirstOrDefault(p => p.Id == id));
    public Task AddPersonalAsync(PersonalEntry personal) { Personal.Add(personal); return Task.CompletedTask; }
    public Task UpdatePersonalAsync(PersonalEntry personal)
    {
        var idx = Personal.FindIndex(p => p.Id == personal.Id);
        if (idx >= 0) Personal[idx] = personal;
        return Task.CompletedTask;
    }
    public Task DeletePersonalAsync(string id) { Personal.RemoveAll(p => p.Id == id); return Task.CompletedTask; }

    public Task<List<DogEntry>> GetDogListAsync() => Task.FromResult(Dogs.ToList());
    public Task<DogEntry?> GetDogByIdAsync(string id) =>
        Task.FromResult(Dogs.FirstOrDefault(d => d.Id == id));
    public Task AddDogAsync(DogEntry dog) { Dogs.Add(dog); return Task.CompletedTask; }
    public Task UpdateDogAsync(DogEntry dog)
    {
        var idx = Dogs.FindIndex(d => d.Id == dog.Id);
        if (idx >= 0) Dogs[idx] = dog;
        return Task.CompletedTask;
    }
    public Task DeleteDogAsync(string id) { Dogs.RemoveAll(d => d.Id == id); return Task.CompletedTask; }

    public Task<List<DroneEntry>> GetDroneListAsync() => Task.FromResult(Drones.ToList());
    public Task<DroneEntry?> GetDroneByIdAsync(string id) =>
        Task.FromResult(Drones.FirstOrDefault(d => d.Id == id));
    public Task AddDroneAsync(DroneEntry drone) { Drones.Add(drone); return Task.CompletedTask; }
    public Task UpdateDroneAsync(DroneEntry drone)
    {
        var idx = Drones.FindIndex(d => d.Id == drone.Id);
        if (idx >= 0) Drones[idx] = drone;
        return Task.CompletedTask;
    }
    public Task DeleteDroneAsync(string id) { Drones.RemoveAll(d => d.Id == id); return Task.CompletedTask; }

    public Task<SessionData> LoadSessionDataAsync() => Task.FromResult(new SessionData());
    public Task SaveSessionDataAsync(SessionData sessionData) => Task.CompletedTask;
}

/// <summary>In-Memory-Stub für IArchivService – für Unit-Tests ohne Datei-I/O.</summary>
internal sealed class FakeArchivService : IArchivService
{
    private readonly List<ArchivedEinsatz> _archiv = new();

    /// <summary>Fügt direkt einen archivierten Einsatz ein (für Test-Setup).</summary>
    public void Add(ArchivedEinsatz archived) => _archiv.Add(archived);

    public Task<ArchivedEinsatz> ArchiveEinsatzAsync(
        EinsatzData einsatzData, string ergebnis, string bemerkungen,
        List<string>? personalVorOrt = null, List<string>? hundeVorOrt = null)
    {
        var archived = ArchivedEinsatz.FromEinsatzData(einsatzData, ergebnis, bemerkungen, DateTime.Now);
        _archiv.Add(archived);
        return Task.FromResult(archived);
    }

    public Task<List<ArchivedEinsatz>> GetAllArchivedAsync() => Task.FromResult(_archiv.ToList());

    public Task<ArchivedEinsatz?> GetByIdAsync(string id) =>
        Task.FromResult(_archiv.FirstOrDefault(a => a.Id == id));

    public Task<List<ArchivedEinsatz>> SearchAsync(ArchivSearchCriteria criteria) =>
        Task.FromResult(_archiv.ToList());

    public Task<bool> DeleteAsync(string id)
    {
        _archiv.RemoveAll(a => a.Id == id);
        return Task.FromResult(true);
    }

    public Task<byte[]> ExportAllAsJsonAsync() => Task.FromResult(Array.Empty<byte>());
    public Task<int> ImportFromJsonAsync(byte[] jsonData) => Task.FromResult(0);

    public Task UpdateArchivedEinsatzAsync(ArchivedEinsatz archived)
    {
        var idx = _archiv.FindIndex(a => a.Id == archived.Id);
        if (idx >= 0) _archiv[idx] = archived;
        return Task.CompletedTask;
    }

    public Task<ArchivedEinsatz> ImportPacketAsNewEinsatzAsync(
        EinsatzExportPacket packet, string einsatzort = "", string ergebnis = "", string bemerkungen = "")
    {
        var archived = new ArchivedEinsatz { Id = Guid.NewGuid().ToString(), Einsatzort = einsatzort };
        _archiv.Add(archived);
        return Task.FromResult(archived);
    }

    public Task<ArchivStatistics> GetStatisticsAsync() => Task.FromResult(new ArchivStatistics());
}

// ─────────────────────────────────────────────────────────────────────────────
// Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Unit-Tests für EinsatzMergeService.RevertMergeAsync.
/// Prüft alle Edge Cases, bei denen ein fehlerhafter Revert die Datenbank korrumpieren könnte.
/// </summary>
public class EinsatzMergeServiceRevertTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Erstellt einen frischen Service mit isolierten In-Memory-Fakes.
    /// Der EinsatzService wird absichtlich OHNE aktiven Einsatz gestartet
    /// (Einsatzort leer → IsEinsatzAktiv() = false), damit RemoveTeamAsync nicht wirft.
    /// </summary>
    private static (EinsatzMergeService merge, EinsatzService einsatz, FakeMasterDataService md, FakeArchivService archiv)
        CreateServices()
    {
        var einsatz = new EinsatzService();
        var md = new FakeMasterDataService();
        var archiv = new FakeArchivService();
        var merge = new EinsatzMergeService(einsatz, md, archiv);
        return (merge, einsatz, md, archiv);
    }

    /// <summary>Erstellt ein minimales Export-Paket mit genau einem Team und einer Notiz.</summary>
    private static EinsatzExportPacket MinimalPacket(string? teamId = null) => new()
    {
        Label = "Unterabschnitt Nord",
        EinsatzNummer = "2026-001",
        Teams = new List<Team>
        {
            new Team { TeamId = teamId ?? Guid.NewGuid().ToString(), TeamName = "Alpha-Import" }
        },
        Notes = new List<GlobalNotesEntry>
        {
            new GlobalNotesEntry { Id = Guid.NewGuid().ToString(), Text = "Importierte Notiz" }
        }
    };

    /// <summary>
    /// Erzeugt eine vollständig entschiedene Session (alle MasterData-Items = Skip),
    /// ruft RebuildIdRemapping auf und gibt die Session zurück.
    /// </summary>
    private static async Task<EinsatzMergeSession> PrepareSessionAsync(
        EinsatzMergeService svc, EinsatzExportPacket packet,
        MergeDecision defaultDecision = MergeDecision.Skip,
        string? archivedId = null)
    {
        var session = await svc.CreateSessionAsync(packet, archivedId);
        foreach (var item in session.AllMasterDataItems)
            item.Decision = defaultDecision;
        svc.RebuildIdRemapping(session);
        return session;
    }

    // ── Test 1: Teams ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Revert_AddedTeams_AreRemovedFromActiveEinsatz()
    {
        var (merge, einsatz, _, _) = CreateServices();

        // Vor dem Import: 1 Original-Team
        var originalTeam = new Team { TeamId = "orig-1", TeamName = "Original" };
        await einsatz.AddTeamAsync(originalTeam);

        var packet = MinimalPacket();
        var session = await PrepareSessionAsync(merge, packet);
        var entry = await merge.ApplyMergeAsync(session);

        Assert.Equal(2, einsatz.Teams.Count);

        await merge.RevertMergeAsync(entry.MergeId);

        // Nach dem Revert: nur noch das Original-Team
        Assert.Single(einsatz.Teams);
        Assert.Equal("orig-1", einsatz.Teams[0].TeamId);
    }

    // ── Test 2: Notizen ────────────────────────────────────────────────────────

    [Fact]
    public async Task Revert_AddedNotes_AreRemovedFromActiveEinsatz()
    {
        var (merge, einsatz, _, _) = CreateServices();

        var packet = MinimalPacket();
        var importedNoteId = packet.Notes[0].Id;

        var session = await PrepareSessionAsync(merge, packet);
        var entry = await merge.ApplyMergeAsync(session);

        Assert.Contains(einsatz.GlobalNotes, n => n.Id == importedNoteId);

        await merge.RevertMergeAsync(entry.MergeId);

        // Importierte Notiz muss entfernt sein; automatisch erzeugte System-Notizen dürfen bleiben
        Assert.DoesNotContain(einsatz.GlobalNotes, n => n.Id == importedNoteId);
    }

    // ── Test 3: Suchgebiete ───────────────────────────────────────────────────

    [Fact]
    public async Task Revert_AddedSearchAreas_AreRemovedFromActiveEinsatz()
    {
        var (merge, einsatz, _, _) = CreateServices();

        // Vorhandenes Gebiet
        await einsatz.AddSearchAreaAsync(new SearchArea { Id = "area-orig", Name = "Wald Nord" });

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            SearchAreas = new List<SearchArea>
            {
                new SearchArea { Id = Guid.NewGuid().ToString(), Name = "Importgebiet Süd" }
            }
        };

        var session = await PrepareSessionAsync(merge, packet);
        var entry = await merge.ApplyMergeAsync(session);

        Assert.Equal(2, einsatz.CurrentEinsatz.SearchAreas.Count);

        await merge.RevertMergeAsync(entry.MergeId);

        Assert.Single(einsatz.CurrentEinsatz.SearchAreas);
        Assert.Equal("area-orig", einsatz.CurrentEinsatz.SearchAreas[0].Id);
    }

    // ── Test 4: Karten-Marker ─────────────────────────────────────────────────

    [Fact]
    public async Task Revert_AddedMapMarkers_AreRemovedFromActiveEinsatz()
    {
        var (merge, einsatz, _, _) = CreateServices();

        var markerId = Guid.NewGuid().ToString();
        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            MapMarkers = new List<MapMarker>
            {
                new MapMarker { Id = markerId, Label = "Import-Marker", Latitude = 48.1, Longitude = 11.5 }
            }
        };

        var session = await PrepareSessionAsync(merge, packet);
        var entry = await merge.ApplyMergeAsync(session);

        Assert.Contains(await einsatz.GetMapMarkersAsync(), m => m.Id == markerId);

        await merge.RevertMergeAsync(entry.MergeId);

        Assert.DoesNotContain(await einsatz.GetMapMarkersAsync(), m => m.Id == markerId);
    }

    // ── Test 5: GPS-Tracks ────────────────────────────────────────────────────

    [Fact]
    public async Task Revert_AddedGpsTracks_AreRemovedFromActiveEinsatz()
    {
        var (merge, einsatz, _, _) = CreateServices();

        var trackId = Guid.NewGuid().ToString("N");
        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            TrackSnapshots = new List<TeamTrackSnapshot>
            {
                new TeamTrackSnapshot { Id = trackId, TeamName = "Alpha" }
            }
        };

        var session = await PrepareSessionAsync(merge, packet);
        var entry = await merge.ApplyMergeAsync(session);

        Assert.Contains(einsatz.CurrentEinsatz.TrackSnapshots, t => t.Id == trackId);

        await merge.RevertMergeAsync(entry.MergeId);

        Assert.DoesNotContain(einsatz.CurrentEinsatz.TrackSnapshots, t => t.Id == trackId);
    }

    // ── Test 6: Hund nur im Unterabschnitt (CreateNew) ────────────────────────

    [Fact]
    public async Task Revert_DeletesNewlyCreatedDog_WhenDogOnlyInSubgroup()
    {
        var (merge, _, md, _) = CreateServices();

        // Kein Hund in den Stammdaten – nur im Import-Paket
        var importedDog = new DogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Rex",
            Specializations = DogSpecialization.Mantrailing
        };

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Dogs = new List<DogEntry> { importedDog },
            Teams = new List<Team>
            {
                new Team
                {
                    TeamId = Guid.NewGuid().ToString(),
                    TeamName = "Hundeführer Rex",
                    DogId = importedDog.Id,
                    DogName = importedDog.Name
                }
            }
        };

        var session = await merge.CreateSessionAsync(packet);
        // Hund als neu anlegen entscheiden
        Assert.Single(session.DogItems);
        session.DogItems[0].Decision = MergeDecision.CreateNew;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        // Nach Apply: 1 Hund in Stammdaten
        Assert.Single(md.Dogs);
        Assert.Equal("Rex", md.Dogs[0].Name);

        await merge.RevertMergeAsync(entry.MergeId);

        // Nach Revert: Hund wieder aus Stammdaten entfernt
        Assert.Empty(md.Dogs);
    }

    // ── Test 7: Team mit CreateNew-Hund → Hund + Team gemeinsam revertiert ───

    [Fact]
    public async Task Revert_TeamWithCreateNewDog_RemovesTeamAndDog()
    {
        var (merge, einsatz, md, _) = CreateServices();

        var importedDog = new DogEntry { Id = Guid.NewGuid().ToString(), Name = "Balu" };
        var teamId = Guid.NewGuid().ToString();

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Dogs = new List<DogEntry> { importedDog },
            Teams = new List<Team>
            {
                new Team { TeamId = teamId, TeamName = "Balu-Team", DogId = importedDog.Id, DogName = "Balu" }
            }
        };

        var session = await merge.CreateSessionAsync(packet);
        session.DogItems[0].Decision = MergeDecision.CreateNew;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        Assert.Single(md.Dogs);
        Assert.Single(einsatz.Teams);

        await merge.RevertMergeAsync(entry.MergeId);

        Assert.Empty(md.Dogs);
        Assert.Empty(einsatz.Teams);
    }

    // ── Test 8: Personal nur im Unterabschnitt (CreateNew) ───────────────────

    [Fact]
    public async Task Revert_DeletesNewlyCreatedPersonal_WhenPersonalOnlyInSubgroup()
    {
        var (merge, _, md, _) = CreateServices();

        var importedPerson = new PersonalEntry { Id = Guid.NewGuid().ToString(), Vorname = "Max", Nachname = "Mustermann" };

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Personal = new List<PersonalEntry> { importedPerson }
        };

        var session = await merge.CreateSessionAsync(packet);
        session.PersonalItems[0].Decision = MergeDecision.CreateNew;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        Assert.Single(md.Personal);
        Assert.Equal("Max", md.Personal[0].Vorname);

        await merge.RevertMergeAsync(entry.MergeId);

        Assert.Empty(md.Personal);
    }

    // ── Test 9: Drohne nur im Unterabschnitt (CreateNew) ─────────────────────

    [Fact]
    public async Task Revert_DeletesNewlyCreatedDrone_WhenDroneOnlyInSubgroup()
    {
        var (merge, _, md, _) = CreateServices();

        var importedDrone = new DroneEntry { Id = Guid.NewGuid().ToString(), Name = "DJI-Testdrohne", Modell = "Mavic 3" };

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Drones = new List<DroneEntry> { importedDrone }
        };

        var session = await merge.CreateSessionAsync(packet);
        session.DroneItems[0].Decision = MergeDecision.CreateNew;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        Assert.Single(md.Drones);
        await merge.RevertMergeAsync(entry.MergeId);
        Assert.Empty(md.Drones);
    }

    // ── Test 10: Doppelter Revert ist idempotent ──────────────────────────────

    [Fact]
    public async Task Revert_CalledTwice_IsIdempotent()
    {
        var (merge, einsatz, _, _) = CreateServices();

        var originalTeam = new Team { TeamId = "orig-idem", TeamName = "Original" };
        await einsatz.AddTeamAsync(originalTeam);

        var session = await PrepareSessionAsync(merge, MinimalPacket());
        var entry = await merge.ApplyMergeAsync(session);

        // Erster Revert
        await merge.RevertMergeAsync(entry.MergeId);
        Assert.Single(einsatz.Teams);

        // Zweiter Revert darf keinen Fehler werfen und keinen weiteren Schaden anrichten
        await merge.RevertMergeAsync(entry.MergeId);
        Assert.Single(einsatz.Teams);

        // Protokolleintrag: IsReverted bleibt true, kein zweites RevertedAt
        var history = await merge.GetMergeHistoryAsync();
        Assert.True(history[0].IsReverted);
    }

    // ── Test 11: Mehrere Merges → nur gezielter Merge wird rückgängig gemacht ─

    [Fact]
    public async Task Revert_DoesNotRemoveDataFromOtherMerges()
    {
        var (merge, einsatz, _, _) = CreateServices();

        // Erster Merge: Team A
        var packet1 = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Teams = new List<Team> { new Team { TeamId = Guid.NewGuid().ToString(), TeamName = "Team-A" } }
        };
        var session1 = await PrepareSessionAsync(merge, packet1);
        var entry1 = await merge.ApplyMergeAsync(session1);

        // Zweiter Merge: Team B
        var packet2 = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Teams = new List<Team> { new Team { TeamId = Guid.NewGuid().ToString(), TeamName = "Team-B" } }
        };
        var session2 = await PrepareSessionAsync(merge, packet2);
        var entry2 = await merge.ApplyMergeAsync(session2);

        Assert.Equal(2, einsatz.Teams.Count);

        // Nur den zweiten Merge rückgängig machen
        await merge.RevertMergeAsync(entry2.MergeId);

        // Team-A muss noch vorhanden sein, Team-B muss weg sein
        Assert.Single(einsatz.Teams);
        Assert.Equal("Team-A", einsatz.Teams[0].TeamName);
    }

    // ── Test 12: Vollständiger Originalzustand – alle Datentypen kombiniert ───

    [Fact]
    public async Task Revert_RestoresCompleteOriginalState_AllDataTypes()
    {
        var (merge, einsatz, md, _) = CreateServices();

        // Originalzustand aufbauen
        var origTeamId = "team-original";
        await einsatz.AddTeamAsync(new Team { TeamId = origTeamId, TeamName = "Orig-Team" });
        await einsatz.AddSearchAreaAsync(new SearchArea { Id = "area-original", Name = "Orig-Gebiet" });
        await einsatz.AddGlobalNoteAsync("Original-Notiz");

        var origTeamCount = einsatz.Teams.Count;
        var origNoteIds = einsatz.GlobalNotes.Select(n => n.Id).ToHashSet();
        var origAreaIds = einsatz.CurrentEinsatz.SearchAreas.Select(a => a.Id).ToHashSet();

        // Import-Paket: neues Personal, neuer Hund, Team, Notiz, Gebiet, Marker, Track
        var importPerson = new PersonalEntry { Id = Guid.NewGuid().ToString(), Vorname = "Anna", Nachname = "Test" };
        var importDog = new DogEntry { Id = Guid.NewGuid().ToString(), Name = "Loki" };
        var importMarkerId = Guid.NewGuid().ToString();
        var importTrackId = Guid.NewGuid().ToString("N");

        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Label = "Volltest",
            Personal = new List<PersonalEntry> { importPerson },
            Dogs = new List<DogEntry> { importDog },
            Teams = new List<Team>
            {
                new Team { TeamId = Guid.NewGuid().ToString(), TeamName = "Import-Team" }
            },
            Notes = new List<GlobalNotesEntry>
            {
                new GlobalNotesEntry { Id = Guid.NewGuid().ToString(), Text = "Import-Notiz" }
            },
            SearchAreas = new List<SearchArea>
            {
                new SearchArea { Id = Guid.NewGuid().ToString(), Name = "Import-Gebiet" }
            },
            MapMarkers = new List<MapMarker>
            {
                new MapMarker { Id = importMarkerId, Label = "Import-Marker", Latitude = 48.1, Longitude = 11.5 }
            },
            TrackSnapshots = new List<TeamTrackSnapshot>
            {
                new TeamTrackSnapshot { Id = importTrackId, TeamName = "Import-Team" }
            }
        };

        var session = await merge.CreateSessionAsync(packet);
        session.PersonalItems[0].Decision = MergeDecision.CreateNew;
        session.DogItems[0].Decision = MergeDecision.CreateNew;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        // Sicherstellen, dass der Import etwas hinzugefügt hat
        Assert.Equal(origTeamCount + 1, einsatz.Teams.Count);
        Assert.Single(md.Personal);
        Assert.Single(md.Dogs);

        // ── Revert ──
        await merge.RevertMergeAsync(entry.MergeId);

        // Teams: nur Orig-Team
        Assert.Equal(origTeamCount, einsatz.Teams.Count);
        Assert.All(einsatz.Teams, t => Assert.Equal(origTeamId, t.TeamId));

        // Notizen: importierte Notiz entfernt, originale Notizen erhalten
        Assert.DoesNotContain(einsatz.GlobalNotes, n => n.Text == "Import-Notiz");

        // Suchgebiete: nur Orig-Gebiet
        Assert.Equal(origAreaIds, einsatz.CurrentEinsatz.SearchAreas.Select(a => a.Id).ToHashSet());

        // Marker: Import-Marker entfernt
        Assert.DoesNotContain(await einsatz.GetMapMarkersAsync(), m => m.Id == importMarkerId);

        // GPS-Tracks: Import-Track entfernt
        Assert.DoesNotContain(einsatz.CurrentEinsatz.TrackSnapshots, t => t.Id == importTrackId);

        // Stammdaten: frisch angelegtes Personal + Hund entfernt
        Assert.Empty(md.Personal);
        Assert.Empty(md.Dogs);
    }

    // ── Test 13: Archivierter Einsatz – Revert stellt Originalzustand wieder her

    [Fact]
    public async Task Revert_ArchivedEinsatz_RestoresTeamsNotesAndAreas()
    {
        var (merge, _, _, archiv) = CreateServices();

        // Archivierten Einsatz mit einem Original-Team anlegen
        var archived = new ArchivedEinsatz
        {
            Id = Guid.NewGuid().ToString(),
            Einsatzort = "Archiv-Test-Ort",
            Teams = new List<ArchivedTeam>
            {
                new ArchivedTeam { TeamId = "arch-team-orig", TeamName = "Archiv-Orig-Team" }
            },
            GlobalNotesEntries = new List<GlobalNotesEntry>
            {
                new GlobalNotesEntry { Id = "note-orig", Text = "Archiv-Notiz" }
            },
            SearchAreas = new List<SearchArea>
            {
                new SearchArea { Id = "area-arch-orig", Name = "Archiv-Orig-Gebiet" }
            },
            MergeHistory = new List<MergeHistoryEntry>()
        };
        archiv.Add(archived);

        var importNoteId = Guid.NewGuid().ToString();
        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Teams = new List<Team>
            {
                new Team { TeamId = Guid.NewGuid().ToString(), TeamName = "Archiv-Import-Team" }
            },
            Notes = new List<GlobalNotesEntry>
            {
                new GlobalNotesEntry { Id = importNoteId, Text = "Archiv-Import-Notiz" }
            },
            SearchAreas = new List<SearchArea>
            {
                new SearchArea { Id = Guid.NewGuid().ToString(), Name = "Archiv-Import-Gebiet" }
            }
        };

        var session = await PrepareSessionAsync(merge, packet, archivedId: archived.Id);
        var entry = await merge.ApplyMergeAsync(session);

        // Nach Apply: 2 Teams, 2 Notizen, 2 Gebiete im Archiv-Einsatz
        var afterApply = await archiv.GetByIdAsync(archived.Id);
        Assert.Equal(2, afterApply!.Teams.Count);
        Assert.Contains(afterApply.GlobalNotesEntries, n => n.Id == importNoteId);
        Assert.Equal(2, afterApply.SearchAreas.Count);

        // Revert
        await merge.RevertMergeAsync(entry.MergeId, archived.Id);

        // Nach Revert: Originalzustand
        var afterRevert = await archiv.GetByIdAsync(archived.Id);
        Assert.Single(afterRevert!.Teams);
        Assert.Equal("arch-team-orig", afterRevert.Teams[0].TeamId);
        Assert.DoesNotContain(afterRevert.GlobalNotesEntries, n => n.Id == importNoteId);
        Assert.Single(afterRevert.SearchAreas);
        Assert.Equal("area-arch-orig", afterRevert.SearchAreas[0].Id);
    }

    // ── Test 14: Suchgebiet-Namenskonflikt (KeepBoth) → Revert entfernt nur den Import ──

    [Fact]
    public async Task Revert_SearchAreaNameConflict_KeepBoth_RemovesOnlyImportedArea()
    {
        var (merge, einsatz, _, _) = CreateServices();

        // Vorhandenes Gebiet mit identischem Namen
        var origAreaId = "area-keep-orig";
        await einsatz.AddSearchAreaAsync(new SearchArea { Id = origAreaId, Name = "Streitgebiet" });

        // Import hat gleichnamiges Gebiet mit anderer ID → KeepBoth-Konflikt
        var importAreaId = Guid.NewGuid().ToString();
        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            SearchAreas = new List<SearchArea>
            {
                new SearchArea { Id = importAreaId, Name = "Streitgebiet" }
            }
        };

        var session = await PrepareSessionAsync(merge, packet);

        // KeepBoth ist der Default – Session erstellt eine neue ID für das Import-Gebiet
        Assert.Single(session.SearchAreaItems);
        Assert.Equal(SearchAreaConflict.SameNameDifferentId, session.SearchAreaItems[0].ConflictType);
        session.SearchAreaItems[0].NameConflictResolution = SearchAreaNameConflictResolution.KeepBoth;

        var entry = await merge.ApplyMergeAsync(session);

        // Nach Apply: 2 Gebiete vorhanden (beide "Streitgebiet" mit unterschiedlicher ID)
        Assert.Equal(2, einsatz.CurrentEinsatz.SearchAreas.Count);

        await merge.RevertMergeAsync(entry.MergeId);

        // Nach Revert: nur Orig-Gebiet
        Assert.Single(einsatz.CurrentEinsatz.SearchAreas);
        Assert.Equal(origAreaId, einsatz.CurrentEinsatz.SearchAreas[0].Id);
    }

    // ── Test 15: Unbekannte MergeId wirft keinen Fehler ──────────────────────

    [Fact]
    public async Task Revert_WithUnknownMergeId_DoesNotThrow()
    {
        var (merge, _, _, _) = CreateServices();

        // Kein Fehler erwartet, da unbekannte ID einfach ignoriert wird
        var exception = await Record.ExceptionAsync(() =>
            merge.RevertMergeAsync("nicht-existierende-id"));

        Assert.Null(exception);
    }

    // ── Test 16: Hund, der lokal schon vorhanden und verlinkt wurde (LinkToExisting),
    //            darf beim Revert NICHT aus den Stammdaten gelöscht werden ───────

    [Fact]
    public async Task Revert_DoesNotDeleteLinkedExistingDog_WhenDecisionIsLinkToExisting()
    {
        var (merge, _, md, _) = CreateServices();

        // Lokal vorhandener Hund
        var localDog = new DogEntry { Id = Guid.NewGuid().ToString(), Name = "Aska" };
        md.Dogs.Add(localDog);

        var importedDog = new DogEntry { Id = Guid.NewGuid().ToString(), Name = "Aska" }; // gleicher Name → Vorschlag
        var packet = new EinsatzExportPacket
        {
            EinsatzNummer = "2026-001",
            Dogs = new List<DogEntry> { importedDog }
        };

        var session = await merge.CreateSessionAsync(packet);
        // Entscheide: Import-Hund mit lokalem Hund verknüpfen (kein CreateNew!)
        session.DogItems[0].Decision = MergeDecision.LinkToExisting;
        session.DogItems[0].SelectedLocalId = localDog.Id;
        merge.RebuildIdRemapping(session);

        var entry = await merge.ApplyMergeAsync(session);

        // Lokaler Hund muss nach Apply noch da sein
        Assert.Single(md.Dogs);

        await merge.RevertMergeAsync(entry.MergeId);

        // Lokaler Hund darf beim Revert NICHT gelöscht worden sein
        Assert.Single(md.Dogs);
        Assert.Equal(localDog.Id, md.Dogs[0].Id);
    }
}
