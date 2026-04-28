# 🤖 AI Agent Guidelines & Self-Improvement Protocol
## Projekt: Einsatzüberwachung.Server

## 1. Deine Rolle & Der Zweck dieser Datei
Du bist ein KI-Entwicklungsassistent (z.B. Claude Code, GitHub Copilot, Cursor) in diesem Softwareprojekt.
Diese Datei ist deine primäre **"Single Source of Truth"** für unsere Projektstandards, Architektur-Entscheidungen und Best Practices. **Lies diese Datei vor jeder größeren Aufgabe.**

---

## 2. Der "Self-Improvement" Zyklus (HÖCHSTE PRIORITÄT)
Wir möchten, dass du und andere Agenten aus Fehlern lernen. Da du keine sitzungsübergreifende Erinnerung hast, dient diese Datei als dein Langzeitgedächtnis.

**Die Regel:**
Wann immer ein menschlicher Entwickler deinen Code korrigiert, einen Vorschlag ablehnt, dich auf einen Fehler hinweist oder eine Lösung manuell anpasst, bist du verpflichtet, folgendes zu tun:
1. **Analysiere:** Verstehe, *warum* der Mensch deinen Code geändert hat. Was war die zugrundeliegende Design-Entscheidung oder Vorliebe?
2. **Extrahiere:** Formuliere aus dieser Erkenntnis eine kurze, prägnante und allgemeingültige Projekt-Regel.
3. **Aktualisiere:** Wenn du die Fähigkeit hast, Dateien zu bearbeiten (z.B. Claude Code), öffne diese Datei (`AI_AGENT_GUIDELINES.md`), füge die neue Regel unter Sektion 4 ("Erfahrungen & Dynamische Regeln") hinzu und speichere sie.
4. **Bestätige:** Wenn du die Datei nicht selbst speichern kannst (z.B. in manchen Copilot-Chats), generiere den aktualisierten Text für Sektion 4 und fordere den Entwickler auf, ihn zu kopieren.

---

## 3. Allgemeine Projekt-Grundsätze (Statisch)

### Projekt-Überblick
Dieses Projekt ist ein **vollständiger Nachbau** der bestehenden Anwendung **Einsatzüberwachung.Web**
(Repository: `Elemirus1996/Einsatzueberwachung.Web`), angepasst für den Betrieb auf einem **Ubuntu Linux Server**
mit **VPN-geschütztem Zugriff** (nicht öffentlich erreichbar).

Das neue Projekt heißt: **Einsatzüberwachung.Server**

Zusätzlich gibt es eine **separate mobile Version** (PWA), die auf folgende Funktionen reduziert ist:
- Einsatz anlegen
- Team-Übersicht (welche Teams im Einsatz sind)
- Notizen
- Funksprüche

### Tech-Stack
- **Frontend**: Blazor Server (.NET 8) mit Razor Components
- **Backend**: ASP.NET Core (.NET 8) auf Kestrel
- **Datenbank**: Entity Framework Core mit SQLite (Standard) / PostgreSQL (optional)
- **Echtzeit**: SignalR für Live-Updates
- **Karten**: Leaflet.js
- **UI**: Bootstrap 5.3+ mit Dark Mode
- **Deployment**: systemd Service hinter Nginx Reverse Proxy auf Ubuntu 22.04/24.04 LTS
- **VPN**: Zugriff nur über WireGuard/OpenVPN-Netzwerk

### Sprache & Stil
- **UI-Sprache**: Deutsch (identisch zum Original)
- **Code**: Variablen, Klassen, Methoden auf Englisch oder Deutsch (dem Original folgend)
- **Commits**: Deutsch oder Englisch, beschreibend
- **Kommentare**: Klären das *Warum*, nicht das *Was*

### Architektur
```
src/
├── Einsatzueberwachung.Server/   ← ASP.NET Core Hauptanwendung
│   ├── Components/Pages/          ← Razor Pages (alle 15 Seiten)
│   ├── Components/Layout/         ← MainLayout, NavMenu
│   ├── Hubs/                      ← SignalR Hubs
│   └── wwwroot/                   ← CSS, JS, Leaflet
├── Einsatzueberwachung.Domain/   ← Business-Logik (Services, Models, Interfaces)
│   ├── Interfaces/
│   ├── Models/ (+ Enums/)
│   ├── Services/
│   └── Validators/
├── Einsatzueberwachung.Mobile/   ← Separate PWA (nur 4 Funktionen)
└── Einsatzueberwachung.Tests/    ← Unit-Tests
deploy/                            ← nginx, systemd, wireguard, setup.sh
```

### Seiten der Hauptanwendung (ALLE implementieren)
1. **Home.razor** — Startseite/Dashboard
2. **EinsatzStart.razor** — Neuen Einsatz starten
3. **EinsatzMonitor.razor** — Hauptüberwachungsseite (Teams, Timer, Notizen, Funksprüche)
4. **EinsatzKarte.razor** — Interaktive Karte (Leaflet.js)
5. **EinsatzBericht.razor** — Einsatzbericht / PDF-Export
6. **EinsatzArchiv.razor** — Archiv vergangener Einsätze
7. **Stammdaten.razor** — Personal-, Hunde- und Drohnen-Verwaltung
8. **Einstellungen.razor** — QR-Code, Theme, Konfiguration, DB-Verwaltung
9. **Wetter.razor** — Wetteranzeige
10. **PopoutNotes.razor** — Popout-Fenster für Funksprüche & Notizen
11. **PopoutTeams.razor** — Popout-Fenster für Team-Übersicht
12. **MobileConnect.razor** — QR-Code Verbindungsseite für Mobile
13. **MobileDashboard.razor** — Mobiles Dashboard
14. **MobileKarte.razor** — Mobile Kartenansicht
15. **Error.razor** — Fehlerseite

### Sicherheit
- Niemals echte API-Keys, Passwörter oder sensible Daten ausgeben — Platzhalter oder `.env`/`appsettings.Production.json` verwenden
- Keine öffentlichen Endpunkte — ausschließlich VPN-interner Zugriff
- Keine Windows-Registry-Zugriffe, keine IIS-Konfiguration

### Zerstörerische Aktionen
Bevor du Dateien löschst oder massive Refactorings durchführst, die das ganze System betreffen, **frage immer den Menschen um Erlaubnis**.

### DO ✅
- Gleiche Funktionalität und Aussehen wie Einsatzüberwachung.Web
- Gleiche Razor Components (Blazor Server) mit identischer UI
- Gleiche CSS-Klassen und Bootstrap 5.3+ Design
- Gleiche SignalR Echtzeit-Logik
- Gleiche Domain-Modelle (EinsatzData, Team, PersonalEntry, Note, etc.)
- **Linux-kompatible Pfade** (`Path.Combine`, kein Backslash)
- Systemd-fähige Konfiguration (kein interaktiver Modus)
- UFW-Regeln statt Windows-Firewall
- Nginx-kompatibel (Forwarded Headers, WebSocket Support)
- Separate Mobile PWA mit nur den 4 Funktionen

### DON'T ❌
- Keine Windows-spezifischen Pfade (`C:\`, Backslashes)
- Keine PowerShell/Batch-Scripts
- Keine Windows-Registry-Zugriffe
- Keine IIS-Konfiguration
- Keine Desktop-Verknüpfungen (.lnk)
- Keine Inno Setup Installer
- Die Mobile-App darf NICHT alle Funktionen der Desktop-Version haben
- Keine öffentlichen Endpunkte

### Migrations-Reihenfolge
**Phase 1: Grundstruktur** — Solution & Projekte, Domain-Layer, SQLite/EF Core, Program.cs  
**Phase 2: Desktop Web-App** — Layout, Home, EinsatzStart, EinsatzMonitor, SignalR, Karte, Stammdaten, Einstellungen, Bericht, Archiv, Wetter, Popouts, Dark Mode, Keyboard Shortcuts  
**Phase 3: Mobile PWA** — Eigenes Projekt, Bottom-Tab-Bar, 4 Funktionen, PWA Manifest + Service Worker  
**Phase 4: Deployment** — Nginx, systemd, setup.sh, WireGuard, Backup, Health Checks

### Referenz-Repository
- **Repo**: `Elemirus1996/Einsatzueberwachung.Web`
- **Branch**: `main`
- **Commit**: `c028154660dc5e9a78bc10e65ddbe7af607453f4`

---

## 4. Erfahrungen & Dynamische Regeln (Wird von KI gepflegt)
*(Liebe KI, füge hier neue Regeln im Format `[Datum] - [Regel]` hinzu, wenn du von Menschen korrigiert wurdest.)*

* [2026-04-28] - Initialer Start des Agenten-Gedächtnisses. `AI_AGENT_GUIDELINES.md` wurde als zentrale "Single Source of Truth" angelegt und mit `.github/copilot-instructions.md` zusammengeführt.
