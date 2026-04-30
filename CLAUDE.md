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

Runtime state is **singleton in-memory** and persisted to SQLite (`runtime-state.db`) as a JSON blob by `RuntimeStatePersistenceService` (a hosted service). The domain services (`IEinsatzService`, `IMasterDataService`, etc.) are registered as singletons and hold all live state. EF Core (`RuntimeDbContext`) is only used for persistence/reload on startup and for radio messages — **not** for live operational data.

The one exception: `IRadioService` is registered **scoped** (not singleton) and uses EF Core directly for radio message storage and retrieval.

### Real-time Updates

SignalR hub is at `/hubs/einsatz`. `EinsatzHubRelayService` (hosted service) subscribes to domain service events and broadcasts them to connected clients. Blazor pages call service methods directly (server-side) and also subscribe to SignalR events to refresh their state. `AuditLogRelayService` and `CollarTrackingRelayService` follow the same relay pattern.

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
| `IAuditLogService` | In-memory audit log with event broadcasting |
| `ITimeService` | Abstracted clock (`AppTimeService`) — use this in domain services for testability |
| `IRadioService` | Scoped; stores/retrieves radio messages in SQLite via EF Core |

### Testing Pattern

Tests use hand-rolled in-memory fakes (e.g. `FakeMasterDataService`) rather than mocks or a real database. When adding tests, follow this pattern: create a fake that implements the relevant interface with `List<T>` backing stores, wire it into the service under test directly.

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
- **CORS**: Three named policies — `VpnPolicy`, `RestApi`, `TrainingApi` — applied per-controller
- **Response compression**: Brotli + Gzip enabled for Blazor payloads
- **Auto-updates**: `UpdateAutoCheckService` polls GitHub releases; update download/apply via `UpdateController`
- **Health endpoint**: `/health` used by systemd timer (every 2 minutes in production)
- **Swagger UI**: available at `/swagger` in Development mode only

### UI Routes

Main Blazor pages: `/` (home), `/einsatz-monitor` (operations dashboard), `/einsatz-karte` (map), `/einsatz-bericht` (PDF report), `/einsatz-import-export` (merge/import sub-team data), `/einsatz-archiv`, `/stammdaten`, `/einstellungen`, `/wetter`, `/divera-status`, `/audit-log`, `/trainer` (training module), `/mobile` (mobile-optimized views), `/popout-notes`, `/popout-teams` (detachable panels).
