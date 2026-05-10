namespace Einsatzueberwachung.Server.Components.Shared;

public record HilfeEntry(string Titel, string Beschreibung, string[] Klickpfade);

public static class HilfeContent
{
    public static readonly Dictionary<string, HilfeEntry> Eintraege = new()
    {
        ["/"] = new(
            "Home / Dashboard",
            "Das Dashboard ist die Startseite der Anwendung. Hier werden aktive Einsaetze, offene Warnungen und schnelle Navigationsmöglichkeiten angezeigt. Globale Notizen können direkt auf dieser Seite eingesehen und bearbeitet werden. Der aktuelle Einsatzstatus wird oben in der Titelleiste angezeigt.",
            new[]
            {
                "Navigation (links) -> Home",
                "Globale Notizen: Direkt auf der Seite sichtbar und editierbar"
            }),

        ["/einsatz-start"] = new(
            "Neuer Einsatz",
            "Hier wird ein neuer Einsatz angelegt. Einsatzort, Szenario (Mantrailer, Flaeche, Truemmer, Sonstige) und erste Vermissteninformationen werden eingetragen. Das gewaehlte Szenario steuert, welche Features in der App aktiv sind. Ein laufender Einsatz muss zuerst beendet werden, bevor ein neuer angelegt werden kann.",
            new[]
            {
                "Navigation -> Neuer Einsatz",
                "Szenario waehlen -> Einsatzort eintragen -> Vermisste eintragen -> 'Einsatz starten'"
            }),

        ["/einsatz-monitor"] = new(
            "Einsatz-Monitor",
            "Die zentrale Uebersicht aller Teams im laufenden Einsatz. Teams koennen hier gestartet, pausiert und zurueckgemeldet werden. Jede Team-Karte zeigt Laufzeit, Hundename, Spezialisierung und aktuelle Warnungen. Neue Teams lassen sich direkt hinzufuegen und bestehende bearbeiten oder entfernen.",
            new[]
            {
                "Navigation -> Monitor",
                "Team starten: Team-Karte -> 'Start'-Button",
                "Team pausieren: Team-Karte -> 'Pause'-Button",
                "Team zurueckmelden: Team-Karte -> 'Zurueck'-Button",
                "Neues Team: '+ Team hinzufuegen'-Button oben rechts"
            }),

        ["/einsatz-karte"] = new(
            "Einsatz-Karte",
            "Interaktive Leaflet-Karte mit GPS-Tracking aller Teams, Suchgebieten und Kartenpunkten. Die Karte zeigt Live-Positionen der Teams (Handy-Tracking) und Halsbandsender. Suchgebiete koennen als Polygone eingezeichnet werden. Ueber die Tabs wechselt man zwischen GPS-Ansicht, Gebieten und Punkten.",
            new[]
            {
                "Navigation -> Karte",
                "Suchgebiet einzeichnen: Tab 'Gebiete' -> 'Gebiet zeichnen'",
                "Kartenpunkt setzen: Tab 'Punkte' -> Karte anklicken",
                "GPS-Track ansehen: Tab 'GPS'"
            }),

        ["/truemmer"] = new(
            "Truemmer-Lagekarte",
            "Pixel-basierte Lagekarte fuer Truemmer-Szenarien. Ein Drohnenfoto wird als Hintergrund hochgeladen, Suchbereiche werden als Polygone direkt auf dem Bild eingezeichnet. Diese Seite ist nur aktiv, wenn das Szenario 'Truemmer' gewaehlt wurde. Mehrere Karten pro Einsatz sind moeglich.",
            new[]
            {
                "Nur sichtbar bei Szenario 'Truemmer'",
                "Navigation -> Truemmer",
                "Karte hochladen: 'Neue Karte' -> Drohnenfoto auswaehlen (JPG/PNG/WEBP, max. 20 MB)",
                "Bereich einzeichnen: Karte oeffnen -> 'Bereich zeichnen' -> Polygon auf Bild klicken"
            }),

        ["/einsatz-archiv"] = new(
            "Einsatz-Archiv",
            "Uebersicht aller abgeschlossenen Einsaetze. Jeder Einsatz kann geoeffnet, als PDF-Bericht exportiert oder als Backup-Paket heruntergeladen werden. Das Archiv erlaubt auch den Import frueherer Einsaetze zur Nachbereitung.",
            new[]
            {
                "Navigation -> Archiv",
                "Einsatz oeffnen: Zeile anklicken",
                "PDF exportieren: Einsatz oeffnen -> 'PDF exportieren'",
                "Backup herunterladen: Einsatz oeffnen -> 'Backup herunterladen'"
            }),

        ["/einsatz-leitung"] = new(
            "Einsatzleitung",
            "Detailansicht des laufenden Einsatzes mit erweiterter Steuerung. Hier koennen Vermisste verwaltet, Checklisten bearbeitet und EL-Notizen erfasst werden. Auch der Einsatz kann von hier aus beendet werden.",
            new[]
            {
                "Navigation -> Monitor -> 'Zur Einsatzleitung'",
                "Vermisste hinzufuegen: Abschnitt 'Vermisste' -> '+ Vermisster'",
                "Checkliste oeffnen: Vermissten-Karte -> Checklisten-Icon",
                "Einsatz beenden: 'Einsatz beenden'-Button"
            }),

        ["/wetter"] = new(
            "Wetter",
            "Zeigt aktuelle Wetterdaten und Vorhersagen vom Deutschen Wetterdienst (DWD) fuer den Einsatzort. Auch Flugwetter-Informationen fuer Drohneneinsaetze sind verfuegbar. Die Daten werden automatisch aktualisiert.",
            new[]
            {
                "Navigation -> Wetter",
                "Einsatzort muss in den Einstellungen oder beim Einsatz-Start hinterlegt sein"
            }),

        ["/divera"] = new(
            "Divera 24/7",
            "Integration mit dem Alarmierungssystem Divera 24/7. Zeigt aktuelle Alarmierungen, Mitgliederstatus und UCR-Eintraege aus der eigenen Divera-Organisation. Erfordert einen konfigurierten Divera-API-Key in den Einstellungen.",
            new[]
            {
                "Navigation -> Divera 24/7",
                "API-Key konfigurieren: Einstellungen -> Divera"
            }),

        ["/stammdaten"] = new(
            "Stammdaten",
            "Verwaltung aller Stammdaten: Hunde, Personen, Drohnen und Checklisten-Templates. Eintraege koennen angelegt, bearbeitet und geloescht werden. Checklisten-Templates werden pro Szenario hinterlegt und beim Anlegen eines Vermissten automatisch angehaengt.",
            new[]
            {
                "Navigation -> Stammdaten",
                "Tab waehlen: Hunde / Personen / Drohnen / Checklisten",
                "Neuer Eintrag: '+ Hinzufuegen'-Button",
                "Checklisten-Template bearbeiten: Tab 'Checklisten' -> Template anklicken"
            }),

        ["/audit-log"] = new(
            "Audit-Log",
            "Chronologisches Protokoll aller sicherheitsrelevanten Aktionen in der Anwendung. Zeigt, wer wann welche Aenderungen vorgenommen hat. Dient der Nachvollziehbarkeit bei der Einsatznachbereitung.",
            new[]
            {
                "Navigation -> Audit-Log",
                "Filter: Zeitraum oder Aktion eingeben"
            }),

        ["/warnzentrum"] = new(
            "Warnzentrum",
            "Zentrale Uebersicht aller aktiven und vergangenen Warnungen. Warnungen entstehen automatisch bei Ueberschreitung von Team-Timern, Hundepausen oder konfigurierten Schwellwerten. Warnregeln koennen hier konfiguriert werden.",
            new[]
            {
                "Navigation -> Warnzentrum",
                "Warnregel anpassen: Abschnitt 'Regeln' -> Regel anklicken"
            }),

        ["/einstellungen"] = new(
            "Einstellungen",
            "Konfiguration der Anwendung: Staffel-Stammdaten, Einsatzbetrieb, Erscheinungsbild (Theme), Tastenkuerzel, Divera-Integration und Systemwartung. Im Tab 'Tastenkuerzel' lassen sich alle Navigationskuerzel und der Stepper auf 'Neuer Einsatz' frei belegen. Kuerzel werden per Klick auf das Eingabefeld und dann Drücken der gewuenschten Taste aufgezeichnet.",
            new[]
            {
                "Navigation -> Einstellungen",
                "Tab auswaehlen (z.B. Tastenkuerzel) -> Eingabefeld anklicken -> Taste druecken -> wird sofort gespeichert",
                "Tastenkuerzel zuruecksetzen: Reset-Pfeil-Schaltflaeche neben dem Feld oder 'Alle zuruecksetzen'"
            }),

        ["/einsatz-import-export"] = new(
            "Import / Export",
            "Export des laufenden Einsatzes als Backup-Paket (.zip) sowie Import von Einsatz-Paketen anderer Geraete. Unterstuetzt auch den Merge-Workflow: Zwei Einsaetze vom selben Vorfall koennen zusammengefuehrt werden.",
            new[]
            {
                "Navigation -> Import / Export",
                "Export: 'Einsatz exportieren' -> ZIP herunterladen",
                "Import: 'Einsatz importieren' -> ZIP-Datei auswaehlen",
                "Merge: Nach Import mit gleichem Einsatz -> 'Zusammenfuehren'"
            }),
    };
}
