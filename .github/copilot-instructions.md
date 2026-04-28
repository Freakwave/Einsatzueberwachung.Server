# 🚀 Projekt: Einsatzüberwachung.Server — VS Code Copilot Projektanweisung

> **⚠️ Hinweis für alle KI-Agenten:**  
> Die vollständigen und aktuellen Projektregeln, Architektur-Entscheidungen und das Self-Improvement-Protokoll befinden sich in:  
> **[`../AI_AGENT_GUIDELINES.md`](../AI_AGENT_GUIDELINES.md)**  
> Bitte diese Datei vor jeder größeren Aufgabe lesen. Die nachfolgenden Inhalte sind eine Kopie für Copilot-Kompatibilität — maßgeblich ist stets `AI_AGENT_GUIDELINES.md`.

---

## 📋 Projekt-Überblick

Dieses Projekt ist ein **vollständiger Nachbau** der bestehenden Anwendung **Einsatzüberwachung.Web** 
(Repository: `Elemirus1996/Einsatzueberwachung.Web`), angepasst für den Betrieb auf einem **Ubuntu Linux Server** 
mit **VPN-geschütztem Zugriff** (nicht öffentlich erreichbar). 

Das neue Projekt heißt: **Einsatzüberwachung.Server**

Zusätzlich muss es eine **separate mobile Version** geben, die auf folgende Funktionen reduziert ist:
- Einsatz anlegen
- Team-Übersicht (welche Teams im Einsatz sind)
- Notizen
- Funksprüche

---

## 🏗️ Referenz: Bestehende Anwendung (Einsatzüberwachung.Web)

### Technologie-Stack der ALTEN Anwendung
- **Frontend**: Blazor Server/WebAssembly (.NET 8) mit Razor Components
- **Backend**: ASP.NET Core (.NET 8)
- **Datenbank**: Entity Framework Core mit SQLite
- **Echtzeit**: SignalR für Live-Updates
- **Karten**: Leaflet.js
- **UI**: Bootstrap 5.3+ mit Dark Mode
- **Plattform**: Windows 10/11 (PowerShell/Batch Starter)

### Seiten der alten Anwendung (ALLE nachbauen)
1. **Home.razor** — Startseite/Dashboard
2. **EinsatzStart.razor** — Neuen Einsatz starten (Formular mit Einsatzdetails)
3. **EinsatzMonitor.razor** — Hauptüberwachungsseite (Teams, Timer, Notizen, Funksprüche)
4. **EinsatzKarte.razor** — Interaktive Karte (Leaflet.js, Suchgebiete, Marker, Polygone)
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

### Domain-Schicht (1:1 übernehmen und anpassen)
```
Einsatzueberwachung.Domain/
├── Interfaces/        ← Service-Interfaces (IEinsatzService, IMasterDataService, etc.)
├── Models/            ← Datenmodelle (EinsatzData, Team, PersonalEntry, Note, etc.)
│   └── Enums/         ← Enumerations
├── Services/          ← Business-Logik (EinsatzService, MasterDataService, PdfExportService, etc.)
└── Validators/        ← Validierungslogik
```

### Hauptfunktionen (ALLE implementieren)
- **🌓 Dark Mode System**: Vollständig, persistente Einstellungen, Cross-Tab Sync
- **🗺️ Interaktive Karten (Leaflet.js)**: Suchgebiete als Polygone, Marker, Farben, Teams zu Gebieten, Druck-Funktion
- **👥 Team-Management**: Teams anlegen/bearbeiten/löschen, Einsatzzeiten (Start/Ende), Status-Überwachung, Timer mit Farbcodierung (Grün → Orange → Rot), Blink-Animation bei kritischem Status
- **📝 Notizen-System**: Globale & team-spezifische Notizen, Historie mit Zeitstempel, Antwort-System (Threads), Typen (Manuell, System, Warnungen)
- **📻 Funksprüche**: Chronologische Erfassung und Anzeige
- **📊 Echtzeit-Synchronisation**: SignalR Live-Updates, automatische Client-Aktualisierung
- **📄 PDF/Bericht-Export**: Einsatzbericht-Generierung
- **⚙️ Einstellungen**: Theme, QR-Code für Mobile, Einsatz-Konfiguration, DB-Verwaltung
- **🌤️ Wetter**: Wetteranzeige für den Einsatzort
- **📂 Einsatz-Archiv**: Vergangene Einsätze durchsuchen/ansehen
- **👤 Stammdaten**: Personal, Hunde, Drohnen verwalten, Excel-Export
- **⌨️ Keyboard Shortcuts**: Ctrl+H (Home), Ctrl+M (Karte), Ctrl+N (Neue Notiz), Ctrl+T (Neues Team), etc.

---

## 🐧 NEUE Plattform-Anforderungen: Ubuntu Linux Server

### Ziel-Infrastruktur
- **Betriebssystem**: Ubuntu Server 22.04 LTS oder 24.04 LTS
- **Zugriff**: **NUR über VPN** (WireGuard oder OpenVPN) — NICHT öffentlich erreichbar
- **Runtime**: .NET 8 auf Linux
- **Datenbank**: SQLite (wie bisher) ODER PostgreSQL (optional, für bessere Server-Performance)
- **Reverse Proxy**: Nginx als Reverse Proxy vor Kestrel
- **SSL/TLS**: Let's Encrypt Zertifikat über Nginx ODER selbst-signiert für VPN-internen Betrieb
- **Service**: systemd Service Unit für automatischen Start/Neustart

### Technologie-Stack für Einsatzüberwachung.Server
- **Frontend**: Blazor Server (.NET 8) — GLEICHE Razor Components wie Original
- **Backend**: ASP.NET Core (.NET 8) auf Kestrel
- **Datenbank**: SQLite (Standard) mit Option auf PostgreSQL
- **Echtzeit**: SignalR (identisch)
- **Karten**: Leaflet.js (identisch)
- **UI**: Bootstrap 5.3+ mit Dark Mode (identisch)
- **Deployment**: Als systemd Service hinter Nginx Reverse Proxy
- **VPN**: Zugriff nur über VPN-Netzwerk (z.B. WireGuard auf 10.0.0.x Netz)

### Projekt-Struktur (NEU)
```
Einsatzueberwachung.Server/
├── src/
│   ├── Einsatzueberwachung.Server/           ← ASP.NET Core Server-Projekt
│   │   ├── Components/
│   │   │   ├── Layout/                       ← MainLayout.razor, NavMenu.razor
│   │   │   └── Pages/                        ← Alle Seiten (wie Original)
│   │   ├── wwwroot/                          ← Statische Dateien (CSS, JS, Leaflet)
│   │   ├── Hubs/                             ← SignalR Hubs
│   │   ├── Program.cs                        ← Server-Konfiguration
│   │   ├── appsettings.json                  ← Konfiguration
│   │   └── appsettings.Production.json       ← Produktions-Konfiguration
│   ├── Einsatzueberwachung.Domain/           ← Domain-Logik (übernommen + angepasst)
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Validators/
│   ├── Einsatzueberwachung.Mobile/           ← SEPARATE Mobile Web-App (PWA)
│   │   ├── Components/
│   │   │   └── Pages/
│   │   │       ├── MobileHome.razor          ← Mobile Startseite
│   │   │       ├── MobileEinsatzStart.razor  ← Einsatz anlegen (mobil)
│   │   │       ├── MobileTeams.razor         ← Team-Übersicht im Einsatz
│   │   │       ├── MobileNotizen.razor       ← Notizen & Funksprüche
│   │   │       └── MobileLayout.razor        ← Mobile Navigation (Bottom-Tab-Bar)
│   │   ├── wwwroot/
│   │   │   ├── manifest.json                 ← PWA Manifest
│   │   │   └── service-worker.js             ← Offline-Caching
│   │   └── Program.cs
│   └── Einsatzueberwachung.Tests/            ← Unit-Tests
├── deploy/
│   ├── nginx/
│   │   └── einsatzueberwachung.conf          ← Nginx Reverse Proxy Config
│   ├── systemd/
│   │   ├── einsatzueberwachung-server.service ← systemd Service für Hauptapp
│   │   └── einsatzueberwachung-mobile.service ← systemd Service für Mobile
│   ├── wireguard/
│   │   └── wg0.conf.example                  ← WireGuard VPN Beispiel-Config
│   └── setup.sh                              ← Automatisches Server-Setup-Script
├── Einsatzueberwachung.Server.sln
├── README.md
├── DEPLOYMENT.md                             ← Deployment-Anleitung für Ubuntu
└── docker-compose.yml (optional)             ← Für Docker-Deployment
```

---

## 📱 SEPARATE Mobile Version — Anforderungen

### Mobile App: `Einsatzueberwachung.Mobile`
Die mobile Version ist eine **eigenständige Blazor PWA** (Progressive Web App), die als separate Anwendung 
auf einem eigenen Port läuft, aber die **gleiche Datenbank und SignalR-Verbindung** nutzt.

### Funktionsumfang der mobilen Version (NUR DIESE 4 Funktionen):

#### 1. 🚨 Einsatz anlegen
- Vereinfachtes Formular zum Anlegen eines neuen Einsatzes
- Felder: Einsatzart (Einsatz/Übung), Alarmzeit, Einsatzort, Melder, Bemerkungen
- Personal-Auswahl (vereinfacht)
- Touch-optimierte große Buttons

#### 2. 👥 Team-Übersicht (im Einsatz)
- Liste aller aktiven Teams mit Status
- Timer-Anzeige pro Team (Farbcodierung: Grün → Orange → Rot)
- Team-Mitglieder und Ausrüstung anzeigen
- Echtzeit-Updates via SignalR
- Kein Bearbeiten/Hinzufügen — nur Anzeige!

#### 3. 📝 Notizen
- Globale Notizen lesen und erstellen
- Team-spezifische Notizen lesen
- Zeitstempel und Autor
- Einfaches Eingabefeld mit Senden-Button

#### 4. 📻 Funksprüche
- Chronologische Liste aller Funksprüche
- Neuen Funkspruch erfassen
- Echtzeit-Updates via SignalR
- Filter nach Typ (Alle/Funksprüche/Notizen)

### Mobile UI-Anforderungen
- **Bottom Navigation Bar** mit 4 Tabs: Einsatz | Teams | Notizen | Funk
- **Touch-optimiert**: Mindestens 48px Touch-Targets
- **Dark Mode**: Automatisch vom Hauptsystem übernehmen
- **PWA**: Installierbar auf Smartphone-Homescreen
- **Responsive**: Optimiert für 320px - 428px Breite
- **Offline-fähig**: Grundlegende Offline-Anzeige der letzten Daten (Service Worker)
- **Pull-to-Refresh**: Für manuelle Aktualisierung
- **Kein Sidebar/Desktop-Navigation**: Nur Bottom-Tab-Bar

---

## 🔧 Linux-spezifische Anpassungen

### Was sich gegenüber der Windows-Version ändert:
1. **Keine PowerShell/Batch-Scripts** → Ersetzt durch Bash-Scripts und systemd Services
2. **Keine Desktop-Verknüpfungen** → Systemd Service mit Auto-Start
3. **Keine Windows-Firewall** → UFW (Uncomplicated Firewall) Regeln
4. **Kein Inno Setup Installer** → setup.sh Bash-Script für Installation
5. **Pfade**: `/opt/einsatzueberwachung/` statt `C:\Program Files\`
6. **Konfiguration**: Environment-Variablen und appsettings.Production.json
7. **Zertifikate**: Nginx kümmert sich um SSL, Kestrel läuft auf HTTP intern
8. **Logging**: Systemd Journal statt Windows Event Log

### Nginx Konfiguration (Beispiel)
```nginx
# /etc/nginx/sites-available/einsatzueberwachung
server {
    listen 443 ssl;
    server_name einsatz.vpn.local;

    ssl_certificate /etc/ssl/certs/einsatz.crt;
    ssl_certificate_key /etc/ssl/private/einsatz.key;

    # Hauptanwendung
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Mobile App
    location /mobile/ {
        proxy_pass http://localhost:5001/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }

    # SignalR WebSocket Support
    location /hubs/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_cache_bypass $http_upgrade;
    }
}
```

### systemd Service (Beispiel)
```ini
# /etc/systemd/system/einsatzueberwachung-server.service
[Unit]
Description=Einsatzueberwachung Server
After=network.target

[Service]
WorkingDirectory=/opt/einsatzueberwachung/server
ExecStart=/usr/bin/dotnet Einsatzueberwachung.Server.dll
Restart=always
RestartSec=10
SyslogIdentifier=einsatzueberwachung
User=einsatz
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

### VPN-Zugriff (WireGuard Beispiel)
```ini
# /etc/wireguard/wg0.conf (Server)
[Interface]
Address = 10.0.0.1/24
ListenPort = 51820
PrivateKey = <SERVER_PRIVATE_KEY>

# Client: Einsatzleiter Laptop
[Peer]
PublicKey = <CLIENT_PUBLIC_KEY>
AllowedIPs = 10.0.0.2/32

# Client: Smartphone Einsatzkraft 1
[Peer]
PublicKey = <CLIENT2_PUBLIC_KEY>
AllowedIPs = 10.0.0.3/32
```

---

## 📌 Wichtige Regeln für Copilot

### DO (MACHEN):
- ✅ **Gleiche Funktionalität und Aussehen** wie Einsatzüberwachung.Web
- ✅ **Gleiche Razor Components** (Blazor Server) mit identischer UI
- ✅ **Gleiche CSS-Klassen und Bootstrap 5.3+** Design
- ✅ **Gleiche SignalR Echtzeit-Logik**
- ✅ **Gleiche Domain-Modelle** (EinsatzData, Team, PersonalEntry, Note, etc.)
- ✅ **Linux-kompatible Pfade** verwenden (Path.Combine, kein Backslash)
- ✅ **Systemd-fähige** Konfiguration (kein interaktiver Modus)
- ✅ **UFW-Regeln** statt Windows-Firewall
- ✅ **Nginx-kompatibel** (Forwarded Headers, WebSocket Support)
- ✅ **Separate Mobile PWA** mit nur den 4 Funktionen
- ✅ **Deutsche Sprache** in der gesamten UI (wie Original)

### DON'T (NICHT MACHEN):
- ❌ Keine Windows-spezifischen Pfade (`C:\`, Backslashes)
- ❌ Keine PowerShell-Scripts
- ❌ Keine Windows-Registry-Zugriffe
- ❌ Keine IIS-Konfiguration
- ❌ Keine Desktop-Verknüpfungen (.lnk)
- ❌ Keine Inno Setup Installer
- ❌ Die Mobile-App darf NICHT alle Funktionen der Desktop-Version haben
- ❌ Keine öffentlichen Endpunkte — nur VPN-interner Zugriff

---

## 🔄 Migrations-Reihenfolge

Beim Aufbau des Projekts folgende Reihenfolge einhalten:

### Phase 1: Grundstruktur
1. Solution und Projekte erstellen
2. Domain-Layer übernehmen (Models, Interfaces, Services)
3. Datenbank-Konfiguration (SQLite mit EF Core)
4. Program.cs mit Linux-Konfiguration

### Phase 2: Desktop Web-App (Hauptanwendung)
5. Layout und Navigation (MainLayout, NavMenu)
6. Home/Dashboard
7. EinsatzStart (Einsatz anlegen)
8. EinsatzMonitor (Hauptseite mit Teams, Timer, Notizen)
9. SignalR Hub für Echtzeit
10. EinsatzKarte (Leaflet.js)
11. Stammdaten (Personal, Hunde, Drohnen)
12. Einstellungen
13. EinsatzBericht + PDF-Export
14. EinsatzArchiv
15. Wetter
16. Popout-Fenster (Notes, Teams)
17. Dark Mode System
18. Keyboard Shortcuts

### Phase 3: Mobile PWA
19. Eigenes Blazor-Projekt für Mobile
20. Mobile Layout mit Bottom-Tab-Bar
21. MobileEinsatzStart (vereinfacht)
22. MobileTeams (nur Anzeige)
23. MobileNotizen
24. MobileFunksprueche
25. PWA Manifest + Service Worker
26. Shared SignalR-Verbindung zur Hauptapp

### Phase 4: Deployment
27. Nginx Konfiguration
28. systemd Services
29. Setup-Script (setup.sh)
30. WireGuard VPN Konfiguration
31. Backup-Script
32. Monitoring (optional: Health Checks)

---

## 📎 Referenz-Repository

Das Original-Repository zum Nachschauen:
- **Repo**: `Elemirus1996/Einsatzueberwachung.Web`
- **Branch**: `main`
- **Commit**: `c028154660dc5e9a78bc10e65ddbe7af607453f4`

Alle Razor-Pages, CSS-Dateien, JS-Dateien und Domain-Modelle aus diesem Repo sollen als Vorlage dienen.