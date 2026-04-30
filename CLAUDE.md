# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build Einsatzueberwachung.Server.sln

# Run web server (development)
dotnet run --project src/Einsatzueberwachung.Server

# Run Windows desktop GPS tracking client
dotnet run --project src/Einsatzueberwachung.LiveTracking

# Run all tests
dotnet test src/Einsatzueberwachung.Tests

# Run a single test
dotnet test src/Einsatzueberwachung.Tests --filter "FullyQualifiedName~<TestName>"

# Docker (production-like)
docker-compose up
```

## Architecture

This is a .NET 9 rescue operation management system ("Einsatzueberwachung") for German search-and-rescue teams. Four projects:

- **Einsatzueberwachung.Server** — ASP.NET Core 9 Blazor Server app (primary UI) + REST API controllers + SignalR hub
- **Einsatzueberwachung.Domain** — Shared business logic: domain models, service interfaces, service implementations, FluentValidation validators
- **Einsatzueberwachung.LiveTracking** — WPF desktop app (Windows-only) that reads USB GPS collars and streams dog locations to the server via webhook
- **Einsatzueberwachung.Tests** — xUnit tests

### State Management Pattern

Runtime state is **singleton in-memory** and persisted to SQLite (`runtime-state.db`) as a JSON blob by `RuntimeStatePersistenceService` (a hosted service, saves every 3 seconds). The domain services (`IEinsatzService`, `IMasterDataService`, etc.) are registered as singletons and hold all live state. EF Core (`RuntimeDbContext`) is only used for persistence/reload on startup and for radio messages — **not** for live operational data.

The one exception: `IRadioService` is registered **scoped** (not singleton) and uses EF Core directly for radio message storage and retrieval.

Master data (personnel, dogs, drones) and settings are persisted as **human-readable JSON files** in `EINSATZ_DATA_DIR`. Writes use `SemaphoreSlim` for thread-safe concurrent access.

### Domain Events & Real-time Update Flow

Services expose `Action<...>` delegate events on their interfaces (e.g. `TeamAdded`, `CollarLocationReceived`, `OutOfBoundsDetected`). The update chain is:

1. Blazor page calls service method directly (singleton, no HTTP)
2. Service mutates in-memory state and fires its `Action` event
3. A **Relay hosted service** (`EinsatzHubRelayService`, `CollarTrackingRelayService`, `AuditLogRelayService`) catches the event and publishes `einsatz:update` to all SignalR clients
4. Blazor page's SignalR handler calls `await InvokeAsync(StateHasChanged)`

Relay services use fire-and-forget (`_ = PublishAsync(...)`) to avoid blocking the event caller.

### Blazor Page Pattern

All interactive Blazor pages use `@rendermode InteractiveServer` and implement `IAsyncDisposable`:

```csharp
@implements IAsyncDisposable

private HubConnection? _hubConnection;

protected override async Task OnInitializedAsync()
{
    _hubConnection = new HubConnectionBuilder()
        .WithUrl(Navigation.ToAbsoluteUri("/hubs/einsatz"))
        .WithAutomaticReconnect()
        .Build();

    _hubConnection.On<string, string>("einsatz:update", async (eventName, json) =>
    {
        // update local state from json or re-read from singleton service
        await InvokeAsync(StateHasChanged);
    });

    await _hubConnection.StartAsync();
}

async ValueTask IAsyncDisposable.DisposeAsync()
{
    if (_hubConnection is not null)
        await _hubConnection.DisposeAsync();
}
```

### Service Layer

All domain services are defined as interfaces in `Domain/Interfaces/` and implemented in `Domain/Services/`. Key services:

| Interface | Responsibility |
|---|---|
| `IEinsatzService` | Active rescue operation: teams, timers, search areas, notes, ELW position, dog pauses |
| `IMasterDataService` | Stammdaten: personnel, dogs, drones (persisted as JSON files) |
| `IArchivService` | Save/load completed operations as JSON archives |
| `ICollarTrackingService` | Real-time GPS collar tracking, track snapshots, boundary warnings |
| `IEinsatzMergeService` | Import sub-team Einsatz exports; apply/revert merge sessions |
| `IEinsatzExportService` | Export current Einsatz state as a portable packet for sub-teams |
| `IPdfExportService` | PDF report generation (QuestPDF + SkiaSharp map rendering) |
| `IExcelExportService` | Excel export/import of master data (ClosedXML) |
| `IDiveraService` | Divera 24/7 alarm dispatch integration |
| `IWeatherService` | DWD weather via BrightSky API |
| `ISettingsService` | Application settings persisted to JSON |
| `IDashboardLayoutService` | Per-user dashboard panel layout persistence |
| `IAuditLogService` | In-memory audit log (max 2000 entries, not persisted, clears on restart) with event broadcasting |
| `ITimeService` | Abstracted clock (`AppTimeService`) — use this in domain services for testability |
| `IRadioService` | Scoped; stores/retrieves radio messages in SQLite via EF Core |

### Hosted Services

All are registered via `AddHostedService<T>()` and run as background workers:

| Service | Behavior |
|---|---|
| `RuntimeStatePersistenceService` | Saves EinsatzData to SQLite every 3 seconds |
| `TeamTimerTickService` | Increments elapsed times ~every 100ms, updates warning flags |
| `EinsatzHubRelayService` | Domain events → SignalR `einsatz:update` broadcasts |
| `CollarTrackingRelayService` | GPS events → SignalR broadcasts |
| `AuditLogRelayService` | Audit log events → SignalR broadcasts |
| `UpdateAutoCheckService` | Polls GitHub releases every 6 hours |

### FluentValidation

Validators are auto-discovered from the Domain assembly at startup:

```csharp
builder.Services.AddValidatorsFromAssembly(typeof(PersonalEntry).Assembly);
```

Key validators: `TeamValidator` (conditional: Hunde-Teams require DogId, Drohnen-Teams require DroneId; FirstWarning < SecondWarning), `PersonalEntryValidator`, `DogEntryValidator`, `DroneEntryValidator`, `AppSettingsValidator`.

### Testing Pattern

Tests use hand-rolled in-memory fakes rather than mocks or a real database:

```csharp
internal sealed class FakeMasterDataService : IMasterDataService
{
    public List<PersonalEntry> Personal { get; } = new();
    public List<DogEntry> Dogs { get; } = new();
    // all interface members backed by simple Lists
}
```

Wire fakes directly into the service under test. See `EinsatzMergeServiceRevertTests` for a complete example testing merge + full undo across all entity types.

### Merge/Import Workflow

`IEinsatzMergeService` implements a 5-step conflict-resolution + undo mechanism:

1. `ParseExportPacket(byte[])` — deserialize import packet
2. `CreateSessionAsync(packet)` — analyze conflicts, auto-preselect matches (name/ID similarity)
3. `RebuildIdRemapping(session)` — after user resolves conflicts, rebuild ID maps
4. `ApplyMergeAsync(session)` — atomically apply changes, record `MergeHistoryEntry`
5. `RevertMergeAsync(mergeId)` — undo using history (only `CreateNew` data is deleted; linked existing data is never removed)

Conflict decisions per entity: `CreateNew`, `LinkToExisting`, `Skip`, `Update`, `KeepBoth` (renames with `_importiert` suffix).

### GPS Collar Tracking

LiveTracking WPF app → `POST /api/collar/receive-location` → `ICollarTrackingService.ReceiveLocationAsync()`:
- `CollarLocationReceived` event always fires (for live display)
- Location history is only recorded when the assigned team's timer is running
- `OutOfBoundsDetected` fires when dog leaves the team's assigned search area polygon
- On team timer **start**: `ClearCollarHistoryAsync()` resets track
- On team timer **stop**: `SaveTrackSnapshotAsync()` saves full track to `Team.TrackSnapshots`

### Data Directories

Configured via environment variables (defaults in parentheses):
- `EINSATZ_DATA_DIR` (`/opt/einsatzueberwachung/data`) — master data JSON files, archives, settings
- `EINSATZ_REPORTS_DIR` (`/opt/einsatzueberwachung/data/berichte`) — generated PDF reports

### Download Endpoints

Minimal API GET endpoints in `Program.cs` serve generated files directly:
- `/downloads/einsatz-bericht.pdf` — PDF report for active Einsatz
- `/downloads/einsatz-bericht.xlsx` — Excel report for active Einsatz
- `/downloads/einsatz-archiv/{id}.pdf` — PDF for archived Einsatz
- `/downloads/einsatz-archiv.json` — full archive export
- `/downloads/stammdaten.xlsx` / `/downloads/stammdaten-template.xlsx`
- `/downloads/data-backup.zip` — full data directory backup
- `/downloads/livetracking.zip` — LiveTracking WPF app bundle
- `/downloads/app-settings.json`, `/downloads/staffel-settings.json`, `/downloads/staffel-logo`

### Training API

A separate read/write mode for scenario-based exercises is toggled in `appsettings.json` under `TrainingApi`. When enabled, `TrainingController` and `ThreadsController` expose endpoints consumed by an external training app. Password-protected trainer role uses cookie auth (`TrainerAuthController`).

### Key Infrastructure Notes

- **Nginx reverse proxy** terminates SSL; `ForwardedHeadersMiddleware` is configured in `Program.cs` for correct IP/scheme handling behind the proxy
- **CORS**: Three named policies — `VpnPolicy` (default, all origins — VPN enforced at Nginx), `RestApi` (all origins), `TrainingApi` (restricted to `TrainingApi:AllowedOrigins` config) — applied per-controller
- **Response compression**: Brotli + Gzip enabled for Blazor payloads
- **Auto-updates**: `UpdateAutoCheckService` polls GitHub releases; update download/apply via `UpdateController`
- **Health endpoint**: `/health` used by systemd timer (every 2 minutes in production)
- **Swagger UI**: available at `/swagger` in Development mode only

### UI Routes

Main Blazor pages: `/` (home), `/einsatz-monitor` (operations dashboard), `/einsatz-karte` (map), `/einsatz-bericht` (PDF report), `/einsatz-import-export` (merge/import sub-team data), `/einsatz-archiv`, `/stammdaten`, `/einstellungen`, `/wetter`, `/divera-status`, `/audit-log`, `/trainer` (training module), `/mobile` (mobile-optimized views), `/popout-notes`, `/popout-teams` (detachable panels).
