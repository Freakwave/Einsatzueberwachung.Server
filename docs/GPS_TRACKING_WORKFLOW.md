# GPS Live-Tracking — Einsatz-Workflow

Dieses Dokument beschreibt den vollständigen Ablauf des GPS-Halsband-Trackings
im Einsatzbetrieb, insbesondere wenn mehrere Teams nacheinander dasselbe
Halsband nutzen.

---

## Grundprinzip: Halsband pro Suche, nicht pro Team

Ein GPS-Halsband wird **nicht dauerhaft einem Team zugewiesen**. Es wird für
jede einzelne Suche (= ein Start/Stopp-Zyklus) neu vergeben. Nach dem Stopp
eines Teams wird das Halsband automatisch freigegeben und steht sofort für das
nächste Team bereit.

```
Team A  ──── [Halsband X] ──→  Start ──→  Suche ──→  Stopp
                                                         │
                                                ┌────────┘
                                                ↓
                               Halsband X ist wieder frei
                                                │
Team B  ────────────────── ←─── [Halsband X] ──┘
         (oder beliebige
          andere Zuordnung)
```

---

## Ablauf Schritt für Schritt

### 1. Vorbereitung — LiveTracking-App verbinden

1. `Einsatzueberwachung.LiveTracking.exe` starten (Windows-Rechner mit GPS-Empfänger).
2. Server-URL eintragen (z.B. `http://10.0.0.1:5000`) und verbinden.
3. Erkannte Halsbänder erscheinen automatisch im EinsatzMonitor und auf der Karte.

---

### 2. Team starten

1. Im **EinsatzMonitor** auf **Start** klicken.

**Fall A: Kein Halsband zugewiesen**

Ein Dialog erscheint mit der Frage, welches Halsband das Team bekommt.
Die Dropdown-Liste zeigt **nur aktuell freie Halsbänder** (inkl. solcher, die
zuvor schon in einem anderen Team im Einsatz waren).

- **„Zuweisen & starten"** — Halsband wird dem Team zugeordnet, Timer startet,
  bisherige Aufzeichnung des Halsbands wird zurückgesetzt.
- **„Ohne Halsband starten"** — Timer läuft, kein GPS-Track.
- **„Abbrechen"** — nichts passiert.

**Fall B: Halsband bereits zugewiesen** (z.B. manuell im Team-Formular gesetzt)

Der Start erfolgt direkt, die Halsband-History wird zurückgesetzt.

> **Hinweis:** Beim Start wird die bisherige Live-History des Halsbands
> automatisch gelöscht, damit der neue Laufweg sauber von vorne beginnt.
> Bereits gespeicherte Snapshots aus früheren Läufen bleiben erhalten.

---

### 3. Suche läuft — Live-Tracking auf der Karte

1. Zur **Einsatzkarte** wechseln.
2. **GPS**-Button (oben rechts) aktivieren.
3. Im schwebenden **Live-Tracking-Panel** werden alle aktiven Halsbänder mit
   ihrer Teamzuordnung angezeigt.
4. Die Lauflinie auf der Karte wird in der Farbe des zugewiesenen Suchgebiets
   gezeichnet.

**Suchgebiet-Warnung:** Verlässt der Hund sein Suchgebiet, erscheint ein
pulsierender roter Marker und eine Warnung im Panel. Bei Rückkehr verschwindet
die Warnung automatisch.

---

### 4. Team stoppen — Track wird gesichert

1. Im EinsatzMonitor auf **Stopp** klicken.
2. Der aufgezeichnete GPS-Track wird als **Snapshot** gespeichert.
   Der Snapshot enthält: Team, Halsband, Suchgebiet, Startzeit, alle GPS-Punkte,
   Gesamtstrecke und Dauer.
3. Das Halsband wird **automatisch freigegeben** — es ist sofort für ein anderes
   Team verfügbar.
4. Auf der **Einsatzkarte** erscheint der abgeschlossene Track als
   **gedimmte gestrichelte Linie** unter „Abgeschlossene Suchen" im Panel.

---

### 5. Halsband weitergeben und nächste Suche starten

1. Das Halsband physisch dem neuen Hundeführer übergeben.
2. Im EinsatzMonitor beim neuen Team auf **Start** klicken.
3. Halsband im Dialog auswählen → **„Zuweisen & starten"**.
4. Die Lauflinie des alten Teams bleibt als Referenz auf der Karte sichtbar
   (gedimmt, gestrichelt).

---

### 6. Taktische Auswertung auf der Karte

Im **GPS-Panel** (Abschnitt „Abgeschlossene Suchen") wird jeder gespeicherte
Track aufgelistet mit:

| Feld | Inhalt |
|------|--------|
| Team | Name des Teams, das den Track aufgezeichnet hat |
| Halsband | Halsband-ID und Name |
| Suchgebiet | Zugewiesenes Gebiet zum Zeitpunkt der Suche |
| Dauer | Laufzeit dieser Suche |
| Strecke | Zurückgelegte Distanz |

**Tracks ein-/ausblenden:** Mit dem Auge-Symbol (👁) neben jedem Eintrag kann
ein einzelner Track auf der Karte ein- oder ausgeblendet werden. Das hilft bei
der taktischen Auswertung (z.B. welche Bereiche wurden bereits abgesucht).

Auf Basis der sichtbaren Laufwege können taktische Entscheidungen getroffen
werden:

- Suchgebiet mit neuen Grenzen neu definieren (nicht abgesuchte Bereiche)
- Nächstes Team gezielt in Lücken schicken
- Überlappungen vermeiden

---

### 7. Einsatzbericht und PDF-Export

1. Im Hauptmenü auf **Bericht** klicken.
2. Ergebnis und Bemerkungen eintragen.
3. Checkbox **„GPS-Tracks in PDF einschliessen"** aktivieren.
   Ein Badge zeigt die Anzahl der gespeicherten Snapshots.
4. **„PDF erzeugen"** klicken.

Jeder Start/Stopp-Zyklus erscheint als **eigener Abschnitt** im PDF:

- Kartenansicht mit dem Laufweg
- Tabelle: Team, Halsband, Suchgebiet, Strecke, Dauer, Punktanzahl
- Suchgebiet-Polygon als Referenz

> Auch wenn dasselbe Halsband von mehreren Teams genutzt wurde, wird jeder
> Lauf korrekt mit dem jeweiligen Team und Zeitstempel dargestellt.

---

### 8. Einsatz beenden

- **„Einsatz beenden und archivieren"** — alle Tracks werden mit dem Einsatz
  archiviert.
- **„Archivieren und neuen Einsatz vorbereiten"** — System wird zurückgesetzt,
  Halsbänder werden neu registriert sobald die LiveTracking-App wieder sendet.

---

## Häufige Situationen

### Halsband wechselt den Hund innerhalb eines Einsatzes

> Team Alpha läuft mit Halsband 001, wird gestoppt.
> Halsband 001 wird physisch auf Hund „Rex" (Team Bravo) umgelegt.
> Team Bravo startet → Halsband 001 im Dialog auswählen → Zuweisen & starten.
> Ergebnis: Zwei separate Snapshots in EinsatzData.TrackSnapshots, einer für Alpha, einer für Bravo.

### Halsband-Akku leer, Ersatz-Halsband

> Team Charlie läuft mit Halsband 002, Akku geht zur Neige.
> Halsband 003 wird bereitgestellt.
> Team Charlie stoppen → 002 wird freigegeben.
> Team Charlie wieder starten → Halsband 003 im Dialog auswählen.
> Der neue Snapshot enthält Halsband 003, der alte Snapshot 002 — beide bleiben erhalten.

### Start ohne Halsband (kein Empfänger verfügbar)

> Im Start-Dialog „Ohne Halsband starten" wählen.
> Team läuft, aber kein GPS-Track wird aufgezeichnet.
> Nach dem Stopp wird kein Snapshot angelegt (keine Punkte vorhanden).

---

## Technische Übersicht (für Entwickler)

```
StartTeamAsync()
  └─ team.CollarId leer && Hundeteam?
       ├─ JA  → CollarSelectModal anzeigen (wartet auf Benutzereingabe)
       │         └─ CollarSelectConfirmAsync()
       │              ├─ AssignCollarToTeamAsync(collarId, teamId)
       │              ├─ ClearCollarHistory(collarId)       ← JS: clearTrack
       │              └─ StartTeamTimerAsync(teamId)
       └─ NEIN → ClearCollarHistory(collarId) + StartTeamTimerAsync(teamId)

StopTeamAsync()
  ├─ GetLocationHistory(collarId)
  ├─ new TeamTrackSnapshot { Id = Guid, CollarId, TeamId, Points, ... }
  ├─ team.TrackSnapshots.Add(snapshot)
  ├─ EinsatzData.TrackSnapshots.Add(snapshot)
  ├─ CollarTrackingService.NotifySnapshotSaved(snapshot)   ← Event → EinsatzKarte
  ├─ UnassignCollarAsync(collarId)                         ← Halsband freigeben
  └─ StopTeamTimerAsync(teamId)

EinsatzKarte: OnTrackSnapshotSaved(snapshot)
  └─ CollarTracking.addCompletedTrack(mapId, id, points, color, teamName, collarName)
     → gedimmte gestrichelte Polyline + Start/End-Marker auf der Karte
```

---

*Dokument erstellt: April 2026 — basierend auf Erkenntnissen aus der Einsatzübung.*
