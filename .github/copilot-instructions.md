# Einsatzüberwachung.Server — Copilot Anweisung

> **Die vollständigen Regeln, Architektur-Entscheidungen und das Self-Improvement-Protokoll stehen in [`../AI_AGENT_GUIDELINES.md`](../AI_AGENT_GUIDELINES.md). Bitte diese Datei vor jeder Aufgabe lesen.**

---

## Projekt

**Einsatzüberwachung.Server** — Blazor Server (.NET 8/9) Einsatzleit-Anwendung für Suchhundestaffeln.
Ubuntu Linux, Nginx Reverse Proxy, WireGuard VPN (kein öffentlicher Zugriff). Migration ist **abgeschlossen**.

---

## Wichtigste Konventionen (Kurzfassung)

### DO ✅
- Linux-Pfade: `AppPathResolver.GetDataDirectory()` + `Path.Combine()` — niemals absolute Pfade hardcoden
- Neue REST-Controller in `Controllers/` (nicht Minimal-API, außer bestehende Download-Endpoints)
- `IDbContextFactory<RuntimeDbContext>` in Singletons — niemals `DbContext` direkt injizieren
- SignalR-Events nur über Relay-Services (`EinsatzHubRelayService`, `CollarTrackingRelayService`)
- Neue JS-Module als eigene Dateien in `wwwroot/js/` (kein inline-JS in `.razor`)
- Trainer-Bereich mit `[Authorize(Policy = "TrainerOnly")]` absichern + `TrainerLayout`
- **Boy Scout Rule**: Jede berührte Datei ein kleines bisschen besser hinterlassen (Methoden extrahieren, Duplikate entfernen, Magic Strings durch Konstanten ersetzen, `private`/`readonly` ergänzen)
- `UseForwardedHeaders()` als erstes in der Middleware-Pipeline

### DON'T ❌
- Keine Windows-Pfade (`C:\`, Backslash), keine PowerShell-Scripts, keine IIS-Konfiguration
- Divera-API-Key niemals in `appsettings.json` — nur via `ISettingsService` / `StaffelSettings.json`
- Kein JS-Code direkt in `.razor`-Dateien einbetten
- `EinsatzHub` nie direkt aus Domain-Services aufrufen — immer über Relay-Services
- `Einsatzueberwachung.LiveTracking` (WPF) — dort keine Linux-APIs; nur HTTP-Kommunikation mit Server
- Keine öffentlichen Endpunkte — nur VPN-interner Zugriff
- Keine großen Refactorings ohne Rückfrage beim Menschen (Umbenennung öffentlicher APIs, Namespace-Moves, DB-Schema, `Program.cs`-Pipeline)

