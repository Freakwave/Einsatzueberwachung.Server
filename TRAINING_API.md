# Training API - Endpunkte fuer Senden und Empfangen

Diese Datei beschreibt die API-Adressen, ueber die die Trainings-App Daten vom Server lesen und an den Server senden kann.

## Basisadresse

- Beispiel Server: http://10.10.0.1
- API-Basis: http://10.10.0.1/api/training

Hinweis:
- Wenn HTTPS aktiv ist, entsprechend https://10.10.0.1 verwenden.
- Wenn `TrainingApi.Enabled=false`, liefern alle Endpunkte `404 Not Found`.
- Schreibend ist nur erlaubt, wenn `TrainingApi.AllowWriteOperations=true`.

## Nur lesen (Trainings-App empfaengt Daten)

### 1) Health
- Methode: `GET`
- Adresse: `/api/training/health`
- Zweck: Verfuegbarkeit, API-Version und Server-Version abrufen.

### 2) Capabilities
- Methode: `GET`
- Adresse: `/api/training/capabilities`
- Zweck: Zeigt, welche Funktionen gerade aktiviert sind (inkl. Write-Faehigkeiten).

### 3) Personal
- Methode: `GET`
- Adresse: `/api/training/personnel`
- Zweck: Stammdaten Personal fuer die Trainings-App.

### 4) Hunde
- Methode: `GET`
- Adresse: `/api/training/dogs`
- Zweck: Stammdaten Hunde fuer die Trainings-App.

### 5) Drohnen
- Methode: `GET`
- Adresse: `/api/training/drones`
- Zweck: Stammdaten Drohnen fuer die Trainings-App.

### 6) Teams
- Methode: `GET`
- Adresse: `/api/training/teams`
- Zweck: Aktuelle Team-Sicht fuer Trainingsabgleich.

### 7) Ressourcen-Snapshot
- Methode: `GET`
- Adresse: `/api/training/resources`
- Zweck: Konsistenter Snapshot mit Personal, Hunden, Drohnen und Teams.

## Schreiben (Trainings-App sendet Daten)

Wichtig:
- Alle Schreiboperationen brauchen `isTraining=true` im Request.
- Bei deaktiviertem Schreiben (`TrainingApi.AllowWriteOperations=false`) kommt `403 Forbidden`.

### 1) Trainingslauf anlegen
- Methode: `POST`
- Adresse: `/api/training/exercises`
- Zweck: Neuen Uebungslauf erzeugen.

Beispiel-Body:
```json
{
  "externalReference": "train-2026-04-25-001",
  "name": "Flachsuche Waldabschnitt Nord",
  "scenario": "Vermeintlich vermisste Person nach Nachtwanderung",
  "location": "Musterwald, Sektor Nord",
  "plannedStartUtc": "2026-04-25T08:30:00Z",
  "isTraining": true,
  "initiator": "TrainingApp"
}
```

### 2) Ereignis spiegeln
- Methode: `POST`
- Adresse: `/api/training/exercises/{exerciseId}/events`
- Zweck: Lagemeldung oder Ereignis zum Trainingslauf senden.

Beispiel-Body:
```json
{
  "type": "lage",
  "text": "Team 2 meldet Sichtung eines Kleidungsstuecks.",
  "occurredAtUtc": "2026-04-25T09:12:00Z",
  "isTraining": true,
  "sourceSystem": "TrainingApp",
  "sourceUser": "uebungsleiter"
}
```

### 3) Entscheidung spiegeln
- Methode: `POST`
- Adresse: `/api/training/exercises/{exerciseId}/decisions`
- Zweck: Fuehrungsentscheidung zum Trainingslauf senden.

Beispiel-Body:
```json
{
  "category": "taktik",
  "decision": "Abschnitt Ost mit Drohne aufklaeren",
  "rationale": "Schnellere Sicht auf unwegsames Gelaende",
  "occurredAtUtc": "2026-04-25T09:20:00Z",
  "isTraining": true,
  "sourceSystem": "TrainingApp",
  "sourceUser": "einsatzleitung"
}
```

### 4) Trainingslauf abschliessen
- Methode: `POST`
- Adresse: `/api/training/exercises/{exerciseId}/complete`
- Zweck: Uebungslauf als abgeschlossen markieren.

Beispiel-Body:
```json
{
  "summary": "Uebung erfolgreich abgeschlossen, Suchkette stabil.",
  "completedAtUtc": "2026-04-25T11:05:00Z",
  "isTraining": true,
  "sourceSystem": "TrainingApp",
  "sourceUser": "uebungsleitung"
}
```

### 5) Trainingsbericht senden
- Methode: `POST`
- Adresse: `/api/training/exercises/{exerciseId}/report`
- Zweck: Abschlussbericht oder Auswertung zum Trainingslauf senden.

Beispiel-Body:
```json
{
  "title": "Uebungsbericht Flachsuche Nord",
  "content": "Positive Teamkommunikation, Verbesserungspotential bei Funkdisziplin.",
  "reportedAtUtc": "2026-04-25T11:30:00Z",
  "isTraining": true,
  "sourceSystem": "TrainingApp",
  "sourceUser": "auswerter"
}
```

## Typische Reihenfolge fuer die Trainings-App

1. `GET /api/training/health`
2. `GET /api/training/capabilities`
3. `GET /api/training/resources`
4. optional `POST /api/training/exercises`
5. optional laufend `POST /api/training/exercises/{exerciseId}/events`
6. optional `POST /api/training/exercises/{exerciseId}/decisions`
7. optional `POST /api/training/exercises/{exerciseId}/complete`
8. optional `POST /api/training/exercises/{exerciseId}/report`

## Fehlerverhalten (kurz)

- `404 Not Found`: Training API deaktiviert oder Exercise-ID existiert nicht.
- `403 Forbidden`: Schreiboperationen sind in der Konfiguration deaktiviert.
- `400 Bad Request`: Request unvollstaendig oder `isTraining` ist nicht `true`.
- `500 Internal Server Error`: Unerwarteter Serverfehler.
