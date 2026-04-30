# CLAUDE.md

Guidance for Claude Code when working in this repository.

---

## Commands

```bash
# Build
dotnet build Einsatzueberwachung.Server.sln

# Run (development, http://localhost:5000, Swagger: /swagger)
dotnet run --project src/Einsatzueberwachung.Server

# Run GPS desktop client (Windows only)
dotnet run --project src/Einsatzueberwachung.LiveTracking

# Run all tests
dotnet test src/Einsatzueberwachung.Tests

# Run single test by name
dotnet test src/Einsatzueberwachung.Tests --filter "FullyQualifiedName~<TestName>"

# Docker (production-like)
docker-compose up
```

---

## Project Structure

```
src/
├── Einsatzueberwachung.Server/     ← Blazor Server UI + REST API + SignalR hub
├── Einsatzueberwachung.Domain/     ← Domain models, interfaces, service implementations, validators
├── Einsatzueberwachung.LiveTracking/ ← WPF desktop app (Windows), USB GPS collar reader
└── Einsatzueberwachung.Tests/      ← xUnit unit tests

deploy/                             ← systemd, nginx, wireguard, backup/update scripts
docs/                               ← GPS workflow, API docs
```

**Rule:** Business logic lives in `Domain`. Never put business logic in Blazor pages or API controllers.

---

## Architecture

### State Management — the most important thing to understand

Runtime state is **singleton in-memory**. Services (`IEinsatzService`, `ICollarTrackingService`, etc.) are registered as **singletons** and hold all live state.

Persistence is handled by `RuntimeStatePersistenceService` (a `BackgroundService`), which serialises the full `EinsatzData` object to SQLite as a JSON blob every 3 seconds. EF Core (`RuntimeDbContext`) is **not** used for live operational queries — only for startup reload and radio message storage.

**Exception:** `IRadioService` is registered **scoped** and uses EF Core directly.

Master data (personnel, dogs, drones) and settings are persisted as human-readable JSON files in `EINSATZ_DATA_DIR`. All file writes go through `SemaphoreSlim` for thread safety.

### Domain Event → SignalR Flow

```
Blazor page calls singleton service
    → service mutates in-memory state
    → service fires Action<...> delegate event
    → Relay hosted service catches event (EinsatzHubRelayService etc.)
    → relay publishes "einsatz:update" to all SignalR clients
    → Blazor page's On handler calls await InvokeAsync(StateHasChanged)
```

Relay services use fire-and-forget (`_ = PublishAsync(...)`) — never `await` inside an event handler to avoid blocking the caller.

### Blazor Page Pattern

Every interactive page uses `@rendermode InteractiveServer` and implements `IAsyncDisposable`.

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
        // re-read from singleton service or deserialise json
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

**Never** call `StateHasChanged()` directly without `InvokeAsync` — SignalR callbacks run on a background thread.

---

## Service Reference

| Interface | Registration | Responsibility |
|-----------|-------------|----------------|
| `IEinsatzService` | Singleton | Active operation: teams, timers, search areas, notes, ELW position, dog pauses |
| `IMasterDataService` | Singleton | Personnel, dogs, drones (JSON files) |
| `IArchivService` | Singleton | Save/load completed operations as JSON archives |
| `ICollarTrackingService` | Singleton | Real-time GPS collar tracking, boundary warnings, track snapshots |
| `IEinsatzMergeService` | Singleton | Import sub-team packets, conflict resolution, undo |
| `IEinsatzExportService` | Singleton | Export current operation state as portable packet |
| `IPdfExportService` | Singleton | PDF report (QuestPDF + SkiaSharp) |
| `IExcelExportService` | Singleton | Excel import/export of master data (ClosedXML) |
| `IDiveraService` | Singleton | Divera 24/7 alarm integration |
| `IWeatherService` | Singleton | DWD weather via BrightSky API |
| `ISettingsService` | Singleton | App settings → JSON |
| `IDashboardLayoutService` | Singleton | Per-user dashboard layout → JSON |
| `IAuditLogService` | Singleton | In-memory audit log, max 2000 entries, not persisted, cleared on restart |
| `ITimeService` | Singleton | Abstracted clock — **always use this in domain services, never `DateTime.Now`** |
| `IRadioService` | **Scoped** | Radio messages → SQLite via EF Core |

### Hosted Services

| Service | Interval / Trigger |
|---------|-------------------|
| `RuntimeStatePersistenceService` | Every 3 seconds |
| `TeamTimerTickService` | ~Every 100ms |
| `EinsatzHubRelayService` | On domain events |
| `CollarTrackingRelayService` | On GPS events |
| `AuditLogRelayService` | On audit events |
| `UpdateAutoCheckService` | Every 6 hours |

---

## GPS Collar Tracking

Flow: `LiveTracking WPF` → `POST /api/collar/receive-location` → `ICollarTrackingService.ReceiveLocationAsync()`

- `CollarLocationReceived` always fires (live display)
- Location history is only recorded while the team's timer is **running**
- `OutOfBoundsDetected` fires when the dog leaves the team's search area polygon
- Timer **start** → `ClearCollarHistoryAsync()` (resets track)
- Timer **stop** → `SaveTrackSnapshotAsync()` (saves full track to `Team.TrackSnapshots`)

---

## Merge/Import Workflow

Five-step process in `IEinsatzMergeService`:

1. `ParseExportPacket(byte[])` — deserialise import packet
2. `CreateSessionAsync(packet)` — analyse conflicts, auto-preselect by name/ID similarity
3. `RebuildIdRemapping(session)` — after user resolves conflicts, rebuild ID maps
4. `ApplyMergeAsync(session)` — atomically apply, record `MergeHistoryEntry`
5. `RevertMergeAsync(mergeId)` — undo: only `CreateNew` data is deleted; linked existing data is **never** removed

Conflict decision types: `CreateNew`, `LinkToExisting`, `Skip`, `Update`, `KeepBoth` (renames with `_importiert` suffix).

---

## Validation

Validators use FluentValidation and are auto-discovered from the Domain assembly:

```csharp
builder.Services.AddValidatorsFromAssembly(typeof(PersonalEntry).Assembly);
```

Key validators: `TeamValidator` (Hunde-Teams require `DogId`; Drohnen-Teams require `DroneId`; `FirstWarning < SecondWarning`), `PersonalEntryValidator`, `DogEntryValidator`, `DroneEntryValidator`, `AppSettingsValidator`.

**Always validate** new entities through their FluentValidation validator before persisting. Do not add ad-hoc `if` checks to services — extend the validator instead.

---

## Testing

Tests use hand-rolled in-memory fakes, not mocks or a real database.

```csharp
internal sealed class FakeMasterDataService : IMasterDataService
{
    public List<PersonalEntry> Personal { get; } = new();
    public List<DogEntry> Dogs { get; } = new();
    // all interface members backed by simple collections
}
```

Wire fakes directly into the service under test. See `EinsatzMergeServiceRevertTests` for a complete example.

**Rules for new tests:**
- One test class per service
- Use `FakeTimeService` (or create one) when testing timer logic — never `DateTime.Now`
- Do not test Blazor components directly — test the service they call
- Test names: `MethodName_Scenario_ExpectedResult`

---

## API Routes

### REST Controllers (`/api/`)

| Endpoint | Purpose |
|----------|---------|
| `POST /api/einsatz/start` | Start new operation |
| `GET /api/einsatz` | Current operation state |
| `POST /api/collar/receive-location` | GPS collar webhook (LiveTracking → Server) |
| `GET /api/radio` | List radio messages |
| `POST /api/radio` | Create radio message |
| `GET /api/divera/status` | Divera 24/7 status |
| `GET /api/training/resources` | Master data snapshot for training app |
| `POST /api/update/check` | Trigger GitHub release check |
| `POST /api/update/install` | Download and apply update |
| `GET /health` | Health check (used by systemd) |

### Download Endpoints (Minimal API, in `Program.cs`)

`/downloads/einsatz-bericht.pdf`, `/downloads/einsatz-bericht.xlsx`, `/downloads/einsatz-archiv/{id}.pdf`, `/downloads/einsatz-archiv.json`, `/downloads/stammdaten.xlsx`, `/downloads/stammdaten-template.xlsx`, `/downloads/data-backup.zip`, `/downloads/livetracking.zip`, `/downloads/app-settings.json`, `/downloads/staffel-settings.json`, `/downloads/staffel-logo`

### SignalR

Hub: `/hubs/einsatz` — event name `einsatz:update`, payload `(string eventName, string json)`

### Blazor Pages

`/` · `/einsatz-monitor` · `/einsatz-karte` · `/einsatz-bericht` · `/einsatz-import-export` · `/einsatz-archiv` · `/stammdaten` · `/einstellungen` · `/wetter` · `/divera-status` · `/audit-log` · `/trainer` · `/mobile` · `/popout-notes` · `/popout-teams`

---

## CORS Policies

| Policy | Scope | Allowed Origins |
|--------|-------|-----------------|
| `VpnPolicy` | Default (all controllers) | All — VPN is enforced at Nginx level |
| `RestApi` | REST-only controllers | All |
| `TrainingApi` | Training endpoints | `TrainingApi:AllowedOrigins` from config |

---

## Infrastructure

- **Nginx** terminates SSL. `ForwardedHeadersMiddleware` is configured in `Program.cs` — do not remove it.
- **Response compression**: Brotli + Gzip enabled — do not add uncompressed static file middleware.
- **Swagger**: only available in `Development` environment at `/swagger`.
- **Data dirs** (configurable via env vars):
  - `EINSATZ_DATA_DIR` (default: `/opt/einsatzueberwachung/data`) — master data JSON, archives, settings
  - `EINSATZ_REPORTS_DIR` (default: `/opt/einsatzueberwachung/data/berichte`) — generated PDFs

---

## Coding Conventions

- **C# version:** latest features enabled (`.NET 9`). Use `required` properties, primary constructors, and collection expressions where they improve clarity.
- **Nullability:** `<Nullable>enable</Nullable>` is on. No `!` null-forgiving operator without a comment explaining why.
- **Async:** All I/O methods are `async Task`. Never use `.Result` or `.Wait()` — deadlocks on Blazor's synchronisation context.
- **Time:** Always inject and use `ITimeService`. Never use `DateTime.Now` or `DateTime.UtcNow` directly in domain services.
- **File writes:** Always acquire the `SemaphoreSlim` before writing master data JSON files. Never write files directly.
- **SignalR callbacks:** Always wrap UI state mutations in `await InvokeAsync(StateHasChanged)`.
- **Logging:** Use `ILogger<T>` injected via constructor. Log at `Information` for state transitions, `Warning` for recoverable errors, `Error` for exceptions.
- **No magic strings:** Event names, route paths, and policy names must be defined as `public const string` in a central location, not inlined.

---

## What NOT to Do

- Do not put `await` inside a domain event handler — use fire-and-forget in relay services.
- Do not inject `IRadioService` as a singleton — it is scoped (EF Core dependency).
- Do not call `EF Core` for live operational data — only for radio messages and startup reload.
- Do not add new JSON file stores without a `SemaphoreSlim` guard.
- Do not read `DateTime.Now` in domain or service code — use `ITimeService`.
- Do not add Swagger annotations or OpenAPI attributes to Blazor pages — only to controllers.
- Do not commit `.csx` scratch files, `appsettings.Development.json` with real credentials, or local SQLite databases.
- Do not use `Thread.Sleep` or synchronous delays in hosted services — use `await Task.Delay`.

---

## Training Module

Toggled via `appsettings.json` under `TrainingApi`. When enabled:
- `TrainingController` and `ThreadsController` expose read/write endpoints for an external training app.
- Trainer authentication is cookie-based (`TrainerAuthController`) with a single password from settings — this is separate from any user auth.

---

## Adding a New Feature — Checklist

1. Define or extend the interface in `Domain/Interfaces/`
2. Implement in `Domain/Services/` — inject `ITimeService`, not `DateTime`
3. Register in `Program.cs` (singleton unless EF Core dependency → scoped)
4. Add FluentValidation validator if new entity is introduced
5. Add relay in `EinsatzHubRelayService` if the feature needs real-time broadcast
6. Add Blazor page under `Server/Components/Pages/` with `@rendermode InteractiveServer` and `IAsyncDisposable`
7. Write tests with a fake implementation of any injected interfaces
8. Update this file if the architecture changes
