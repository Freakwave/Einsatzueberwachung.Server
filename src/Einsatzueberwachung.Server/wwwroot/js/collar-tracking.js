// Live-Tracking von GPS-Halsbändern auf der Leaflet-Karte
// Zeichnet Polylines (Pfade) für zugewiesene Halsbänder und zeigt Warnungen bei Out-of-Bounds

// ── UTM-Konvertierung (WGS84 → UTM) ──────────────────────────────────────────
function latLngToUtm(lat, lng) {
    const a = 6378137.0, f = 1 / 298.257223563;
    const b = a * (1 - f);
    const e2 = 1 - (b * b) / (a * a);
    const e2p = e2 / (1 - e2);
    const k0 = 0.9996;
    const zone = Math.floor((lng + 180) / 6) + 1;
    const lon0 = ((zone - 1) * 6 - 180 + 3) * Math.PI / 180;
    const latR = lat * Math.PI / 180;
    const lngR = lng * Math.PI / 180;
    const N = a / Math.sqrt(1 - e2 * Math.sin(latR) ** 2);
    const T = Math.tan(latR) ** 2;
    const C = e2p * Math.cos(latR) ** 2;
    const A = Math.cos(latR) * (lngR - lon0);
    const n = (a - b) / (a + b);
    const M = a * ((1 - e2 / 4 - 3 * e2 ** 2 / 64 - 5 * e2 ** 3 / 256) * latR
        - (3 * e2 / 8 + 3 * e2 ** 2 / 32 + 45 * e2 ** 3 / 1024) * Math.sin(2 * latR)
        + (15 * e2 ** 2 / 256 + 45 * e2 ** 3 / 1024) * Math.sin(4 * latR)
        - 35 * e2 ** 3 / 3072 * Math.sin(6 * latR));
    const easting = k0 * N * (A + (1 - T + C) * A ** 3 / 6
        + (5 - 18 * T + T ** 2 + 72 * C - 58 * e2p) * A ** 5 / 120) + 500000;
    const northing = k0 * (M + N * Math.tan(latR) * (A ** 2 / 2
        + (5 - T + 9 * C + 4 * C ** 2) * A ** 4 / 24
        + (61 - 58 * T + T ** 2 + 600 * C - 330 * e2p) * A ** 6 / 720))
        + (lat < 0 ? 10000000 : 0);
    const band = 'CDEFGHJKLMNPQRSTUVWXX'[Math.min(Math.floor((lat + 80) / 8), 20)];
    return `${zone}${band} ${Math.round(easting)} ${Math.round(northing)}`;
}

window.CollarTracking = {
    // Gespeicherte Tracking-Daten pro Halsband
    _tracks: {},

    // Abgeschlossene Tracks (Snapshots) pro snapshotId
    _completedTracks: {},

    // Farben für verschiedene Halsbänder
    _colors: [
        '#E63946', '#0077B6', '#2DC653', '#FF851B', '#9B5DE5',
        '#00B4D8', '#F72585', '#FFD166', '#06D6A0', '#FF6B35',
        '#8B4513', '#20B2AA', '#DC143C', '#4169E1', '#228B22',
        '#FF1493', '#DAA520', '#6A0DAD', '#40E0D0', '#CD5C5C'
    ],
    _colorIndex: 0,

    // SignalR-Connection-Referenz
    _connection: null,
    _dotNetRef: null,

    // Initialisiert das Collar-Tracking auf einer bestehenden Karte
    initialize: function (mapId, dotNetReference) {
        this._dotNetRef = dotNetReference;

        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData) {
            console.error('CollarTracking: Karte nicht gefunden:', mapId);
            return false;
        }

        // Alte Tracks zurücksetzen (wichtig bei Seitennavigation, da die alte Karte disposed wird)
        this._tracks = {};
        this._completedTracks = {};
        this._colorIndex = 0;

        // Eigene FeatureGroup für Tracking-Layer
        if (!mapData.trackingLayer) {
            mapData.trackingLayer = new L.FeatureGroup();
            mapData.map.addLayer(mapData.trackingLayer);
        }

        console.log('CollarTracking: Initialisiert auf Karte', mapId);
        return true;
    },

    // Neue GPS-Position empfangen und auf der Karte zeichnen
    // color ist optional – wenn angegeben, wird diese Farbe verwendet/aktualisiert
    // dogLabel ist optional – wird im Popup angezeigt ("Hund (Team)")
    updatePosition: function (mapId, collarId, lat, lng, timestamp, color, dogLabel) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        // Track initialisieren falls neu
        if (!this._tracks[collarId]) {
            this._tracks[collarId] = {
                positions: [],
                polyline: null,
                marker: null,
                color: color || this._colors[this._colorIndex % this._colors.length]
            };
            if (!color) this._colorIndex++;
        }

        // Farbe aktualisieren falls explizit übergeben und geändert
        if (color && this._tracks[collarId].color !== color) {
            this._setTrackColor(mapData, collarId, color);
        }
        // Label aktualisieren falls mitgeliefert
        if (dogLabel) this._tracks[collarId].dogLabel = dogLabel;

        const track = this._tracks[collarId];
        track.positions.push([lat, lng]);

        // Polyline aktualisieren oder erstellen
        if (track.polyline && mapData.trackingLayer.hasLayer(track.polyline)) {
            track.polyline.addLatLng([lat, lng]);
        } else {
            // Polyline neu erstellen (auch wenn alte Referenz existiert aber nicht mehr auf der Karte ist)
            track.polyline = L.polyline(track.positions, {
                color: track.color,
                weight: 3,
                opacity: 0.8,
                dashArray: null
            });
            track.polyline.addTo(mapData.trackingLayer);
        }

        // Aktuellen Positions-Marker aktualisieren
        if (track.marker && mapData.trackingLayer.hasLayer(track.marker)) {
            track.marker.setLatLng([lat, lng]);
        } else {
            const size = track._oobActive ? 36 : 20;
            const half = size / 2;
            const oobRing = track._oobActive
                ? `<circle cx="${half}" cy="${half}" r="${half - 2}" fill="none" stroke="#FF0000" stroke-width="2" opacity="0.6" class="collar-oob-ring"/>`
                : '';
            const icon = L.divIcon({
                html: `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
                    ${oobRing}
                    <circle cx="${half}" cy="${half}" r="8" fill="${track.color}" stroke="white" stroke-width="2"/>
                    <circle cx="${half}" cy="${half}" r="3" fill="white"/>
                </svg>`,
                iconSize: [size, size],
                iconAnchor: [half, half],
                className: track._oobActive ? 'collar-marker-icon collar-oob-active' : 'collar-marker-icon'
            });
            track.marker = L.marker([lat, lng], { icon: icon })
                .bindPopup(`<strong>${track.dogLabel || collarId}</strong><br><small>Halsband: ${collarId}</small><br><small>UTM: ${latLngToUtm(lat, lng)}</small><br><small>${new Date(timestamp).toLocaleTimeString('de-DE')}</small>`);
            track.marker.addTo(mapData.trackingLayer);
        }

        // Popup aktualisieren
        track.marker.setPopupContent(
            `<strong>${track.dogLabel || collarId}</strong><br>` +
            `<small>Halsband: ${collarId}</small><br>` +
            `<small>UTM: ${latLngToUtm(lat, lng)}</small><br>` +
            `<small>Lat: ${lat.toFixed(5)}, Lng: ${lng.toFixed(5)}</small><br>` +
            `<small>${new Date(timestamp).toLocaleTimeString('de-DE')}</small>`
        );
    },

    // Bestehenden Pfadverlauf eines Halsbands laden und zeichnen
    // color ist optional – wenn angegeben, wird diese Farbe verwendet
    loadHistory: function (mapId, collarId, locations, color, dogLabel) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer || !locations || locations.length === 0) return;

        // Track zurücksetzen
        this.clearTrack(mapId, collarId);

        // Initialisieren
        if (!this._tracks[collarId]) {
            this._tracks[collarId] = {
                positions: [],
                polyline: null,
                marker: null,
                color: color || this._colors[this._colorIndex % this._colors.length],
                dogLabel: dogLabel || collarId
            };
            if (!color) this._colorIndex++;
        } else if (color) {
            this._tracks[collarId].color = color;
        }
        if (dogLabel) this._tracks[collarId].dogLabel = dogLabel;

        const track = this._tracks[collarId];
        const positions = locations.map(loc => [loc.latitude, loc.longitude]);
        track.positions = positions;

        if (positions.length > 0) {
            // Polyline zeichnen
            track.polyline = L.polyline(positions, {
                color: track.color,
                weight: 3,
                opacity: 0.8
            });
            track.polyline.addTo(mapData.trackingLayer);

            // Marker an letzter Position
            const lastPos = positions[positions.length - 1];
            const lastLoc = locations[locations.length - 1];
            const icon = L.divIcon({
                html: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
                    <circle cx="10" cy="10" r="8" fill="${track.color}" stroke="white" stroke-width="2"/>
                    <circle cx="10" cy="10" r="3" fill="white"/>
                </svg>`,
                iconSize: [20, 20],
                iconAnchor: [10, 10],
                className: 'collar-marker-icon'
            });
            track.marker = L.marker(lastPos, { icon: icon })
                .bindPopup(`<strong>${track.dogLabel || collarId}</strong><br><small>Halsband: ${collarId}</small><br><small>UTM: ${latLngToUtm(lastPos[0], lastPos[1])}</small><br><small>${new Date(lastLoc.timestamp).toLocaleTimeString('de-DE')}</small>`);
            track.marker.addTo(mapData.trackingLayer);
        }
    },

    // Out-of-Bounds Warnung anzeigen: pulsierender Ring am Track-Kopf
    showOutOfBoundsWarning: function (mapId, collarId, teamId, lat, lng) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData) return;

        const track = this._tracks[collarId];
        if (!track || !track.marker) return;

        // Marker-Icon durch pulsierende Variante ersetzen
        const icon = L.divIcon({
            html: `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" viewBox="0 0 36 36" class="collar-oob-pulse-svg">
                <circle cx="18" cy="18" r="16" fill="none" stroke="#FF0000" stroke-width="2" opacity="0.6" class="collar-oob-ring"/>
                <circle cx="18" cy="18" r="8" fill="${track.color}" stroke="white" stroke-width="2"/>
                <circle cx="18" cy="18" r="3" fill="white"/>
            </svg>`,
            iconSize: [36, 36],
            iconAnchor: [18, 18],
            className: 'collar-marker-icon collar-oob-active'
        });
        track.marker.setIcon(icon);
        track._oobActive = true;

        // Warnung nach 15 Sekunden zurücknehmen (normales Icon wiederherstellen)
        if (track._oobTimeout) clearTimeout(track._oobTimeout);
        track._oobTimeout = setTimeout(() => {
            this._clearOobWarning(mapData, collarId);
        }, 15000);
    },

    // Out-of-Bounds-Puls vom Marker entfernen (intern)
    _clearOobWarning: function (mapData, collarId) {
        const track = this._tracks[collarId];
        if (!track || !track.marker || !track._oobActive) return;

        if (track._oobTimeout) {
            clearTimeout(track._oobTimeout);
            track._oobTimeout = null;
        }

        const icon = L.divIcon({
            html: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
                <circle cx="10" cy="10" r="8" fill="${track.color}" stroke="white" stroke-width="2"/>
                <circle cx="10" cy="10" r="3" fill="white"/>
            </svg>`,
            iconSize: [20, 20],
            iconAnchor: [10, 10],
            className: 'collar-marker-icon'
        });
        track.marker.setIcon(icon);
        track._oobActive = false;
    },

    // Out-of-Bounds-Puls vom Marker entfernen (von Blazor aufrufbar)
    clearOobWarning: function (mapId, collarId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData) return;
        this._clearOobWarning(mapData, collarId);
    },

    // Track eines einzelnen Halsbands löschen
    clearTrack: function (mapId, collarId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        const track = this._tracks[collarId];
        if (track) {
            if (track.polyline) mapData.trackingLayer.removeLayer(track.polyline);
            if (track.marker) mapData.trackingLayer.removeLayer(track.marker);
            delete this._tracks[collarId];
        }
    },

    // Alle Tracks löschen
    clearAll: function (mapId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        mapData.trackingLayer.clearLayers();
        this._tracks = {};
        this._colorIndex = 0;
    },

    // Farbe eines bestehenden Tracks ändern (Polyline + Marker neu zeichnen)
    _setTrackColor: function (mapData, collarId, newColor) {
        const track = this._tracks[collarId];
        if (!track) return;
        track.color = newColor;

        // Polyline-Farbe aktualisieren
        if (track.polyline && mapData.trackingLayer.hasLayer(track.polyline)) {
            track.polyline.setStyle({ color: newColor });
        }

        // Marker-Icon mit neuer Farbe ersetzen
        if (track.marker && mapData.trackingLayer.hasLayer(track.marker)) {
            const icon = L.divIcon({
                html: `<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 20 20">
                    <circle cx="10" cy="10" r="8" fill="${newColor}" stroke="white" stroke-width="2"/>
                    <circle cx="10" cy="10" r="3" fill="white"/>
                </svg>`,
                iconSize: [20, 20],
                iconAnchor: [10, 10],
                className: 'collar-marker-icon'
            });
            track.marker.setIcon(icon);
        }
    },

    // Tracking-Sichtbarkeit umschalten
    toggleVisibility: function (mapId, visible) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        if (visible) {
            if (!mapData.map.hasLayer(mapData.trackingLayer)) {
                mapData.map.addLayer(mapData.trackingLayer);
            }
        } else {
            if (mapData.map.hasLayer(mapData.trackingLayer)) {
                mapData.map.removeLayer(mapData.trackingLayer);
            }
        }
    },

    // Abgeschlossenen Track (Snapshot) auf der Karte einzeichnen (gedimmt, gestrichelt)
    addCompletedTrack: function (mapId, snapshotId, points, color, teamName, collarName) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;
        if (!points || points.length === 0) return;

        // Bereits vorhandenen Eintrag entfernen
        this._removeCompletedTrackLayers(mapData, snapshotId);

        const positions = points.map(p => [p.latitude, p.longitude]);

        const polyline = L.polyline(positions, {
            color: color,
            weight: 4,
            opacity: 0.5,
            dashArray: '6 5'
        });
        polyline.addTo(mapData.trackingLayer);

        // Klick auf Linie → Blazor-Callback um GPS-Tab zu öffnen und Snapshot aufzuklappen
        polyline.on('click', () => {
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnCompletedTrackClicked', snapshotId);
            }
        });
        polyline.on('mouseover', function () { this.setStyle({ weight: 6, opacity: 0.8 }); });
        polyline.on('mouseout', function () { this.setStyle({ weight: 4, opacity: 0.5 }); });

        // Start-Marker (kleines Dreieck)
        const startIcon = L.divIcon({
            html: `<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 14 14">
                <polygon points="7,1 13,13 1,13" fill="${color}" stroke="white" stroke-width="1.5" opacity="0.7"/>
            </svg>`,
            iconSize: [14, 14], iconAnchor: [7, 7], className: 'collar-completed-icon'
        });
        const startMarker = L.marker(positions[0], { icon: startIcon })
            .bindPopup(`<strong>${teamName}</strong><br><small>Start · ${collarName}</small>`);
        startMarker.addTo(mapData.trackingLayer);

        // End-Marker (kleines Quadrat)
        const lastPos = positions[positions.length - 1];
        const endIcon = L.divIcon({
            html: `<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 14 14">
                <rect x="1" y="1" width="12" height="12" fill="${color}" stroke="white" stroke-width="1.5" opacity="0.7"/>
            </svg>`,
            iconSize: [14, 14], iconAnchor: [7, 7], className: 'collar-completed-icon'
        });
        const endMarker = L.marker(lastPos, { icon: endIcon })
            .bindPopup(`<strong>${teamName}</strong><br><small>Ende · ${collarName}</small>`);
        endMarker.addTo(mapData.trackingLayer);

        this._completedTracks[snapshotId] = {
            polyline: polyline,
            startMarker: startMarker,
            endMarker: endMarker,
            visible: true
        };
    },

    // Sichtbarkeit eines abgeschlossenen Tracks umschalten
    toggleCompletedTrack: function (mapId, snapshotId, visible) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        const ct = this._completedTracks[snapshotId];
        if (!ct) return;

        ct.visible = visible;
        [ct.polyline, ct.startMarker, ct.endMarker].forEach(layer => {
            if (!layer) return;
            if (visible) {
                if (!mapData.trackingLayer.hasLayer(layer)) mapData.trackingLayer.addLayer(layer);
            } else {
                if (mapData.trackingLayer.hasLayer(layer)) mapData.trackingLayer.removeLayer(layer);
            }
        });
    },

    // Alle abgeschlossenen Tracks entfernen
    clearAllCompletedTracks: function (mapId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData || !mapData.trackingLayer) return;

        for (const id in this._completedTracks) {
            this._removeCompletedTrackLayers(mapData, id);
        }
        this._completedTracks = {};
    },

    // Intern: Layer eines abgeschlossenen Tracks von der Karte entfernen
    _removeCompletedTrackLayers: function (mapData, snapshotId) {
        const ct = this._completedTracks[snapshotId];
        if (!ct) return;
        [ct.polyline, ct.startMarker, ct.endMarker].forEach(layer => {
            if (layer && mapData.trackingLayer.hasLayer(layer))
                mapData.trackingLayer.removeLayer(layer);
        });
        delete this._completedTracks[snapshotId];
    },

    // Zentriert und zoomt die Karte so, dass der gesamte Live-Track eines Halsbands sichtbar ist
    zoomToCollar: function (mapId, collarId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData) return false;

        const track = this._tracks[collarId];
        if (!track) return false;

        if (track.positions && track.positions.length > 1) {
            // Gesamten Pfad in den Viewport einpassen
            const bounds = L.latLngBounds(track.positions);
            mapData.map.fitBounds(bounds, { padding: [40, 40], maxZoom: 18 });
            if (track.marker) track.marker.openPopup();
            return true;
        }

        // Fallback: nur aktuelle Position
        const pos = track.marker ? track.marker.getLatLng()
            : (track.positions && track.positions.length === 1 ? track.positions[0] : null);
        if (pos) {
            mapData.map.setView(pos, Math.max(mapData.map.getZoom(), 17));
            if (track.marker) track.marker.openPopup();
            return true;
        }

        return false;
    },

    // Zoomt auf einen abgeschlossenen Track (Snapshot) und passt die Bounds an
    zoomToCompletedTrack: function (mapId, snapshotId) {
        const mapData = window.LeafletMap.maps[mapId];
        if (!mapData) return;

        const ct = this._completedTracks[snapshotId];
        if (!ct || !ct.polyline) return;

        const bounds = ct.polyline.getBounds();
        if (bounds.isValid()) {
            mapData.map.fitBounds(bounds, { padding: [50, 50], maxZoom: 18 });
        }
    }
};
