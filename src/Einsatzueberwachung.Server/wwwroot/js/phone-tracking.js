// Handy-GPS-Tracking: Zeigt Team-Telefon-Positionen auf der Karte als Kreise mit Handy-Symbol

window.PhoneTracking = (function () {
    // teamId -> { marker }
    const _markers = {};

    // teamId -> { polyline, color }
    const _tracks = {};

    // Konfiguriertes Symbol ("phone" | "person" | "person_walking" | "radio" | "dot")
    let _humanIcon = 'phone';

    function _getHumanContent() {
        switch (_humanIcon) {
            case 'person':         return '<i class="bi bi-person-fill"></i>';
            case 'person_walking': return '<i class="bi bi-person-walking"></i>';
            case 'radio':          return '<i class="bi bi-broadcast"></i>';
            case 'dot':            return '<span style="width:10px;height:10px;background:#fff;border-radius:50%;display:block;"></span>';
            default:               return '<i class="bi bi-phone-fill"></i>'; // phone
        }
    }

    function setOptions(opts) {
        if (opts && opts.humanIcon) _humanIcon = opts.humanIcon;
    }

    function _getMapData(mapId) {
        return window.LeafletMap?.maps?.[mapId] ?? null;
    }

    function initialize(mapId) {
        const mapData = _getMapData(mapId);
        if (!mapData) return;

        if (!mapData.phoneLayer) {
            mapData.phoneLayer = new L.FeatureGroup();
            mapData.map.addLayer(mapData.phoneLayer);
        }
        // Zustand aus altem Seitenbesuch zurücksetzen
        Object.keys(_markers).forEach(k => delete _markers[k]);
        Object.keys(_tracks).forEach(k => delete _tracks[k]);
    }

    function _createHumanIcon(color) {
        return L.divIcon({
            className: '',
            html: `<div style="width:34px;height:34px;border-radius:50%;background:${color};border:3px solid #fff;box-shadow:0 1px 5px rgba(0,0,0,0.45);display:flex;align-items:center;justify-content:center;color:#fff;font-size:17px;line-height:1;">${_getHumanContent()}</div>`,
            iconSize: [34, 34],
            iconAnchor: [17, 17],
            popupAnchor: [0, -19]
        });
    }

    function _buildPopup(teamName, lat, lng, timestamp) {
        const time = timestamp
            ? new Date(timestamp).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
            : '--:--';
        return `<strong>&#128241; ${teamName}</strong><br>` +
               `<small>Lat: ${lat.toFixed(5)}, Lng: ${lng.toFixed(5)}</small><br>` +
               `<small>Stand: ${time}</small>`;
    }

    function _teamColor(teamId) {
        const colors = ['#1976d2', '#d32f2f', '#388e3c', '#f57c00', '#7b1fa2',
                        '#00838f', '#c2185b', '#5d4037', '#455a64', '#0288d1'];
        let h = 0;
        for (let i = 0; i < teamId.length; i++) h = (h * 31 + teamId.charCodeAt(i)) & 0xffff;
        return colors[h % colors.length];
    }

    function updateMarker(mapId, teamId, teamName, lat, lng, timestamp) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;

        const color = _teamColor(teamId);

        const popupContent = _buildPopup(teamName, lat, lng, timestamp);

        if (_markers[teamId]) {
            _markers[teamId].setLatLng([lat, lng]);
            _markers[teamId].setPopupContent(popupContent);
        } else {
            const icon = _createHumanIcon(color);
            const marker = L.marker([lat, lng], { icon })
                .bindPopup(popupContent);
            marker.addTo(mapData.phoneLayer);
            _markers[teamId] = marker;
        }
    }

    /**
     * Lädt einen bestehenden Telefon-GPS-Track und zeichnet ihn als Polylinie.
     * @param {string} mapId
     * @param {string} teamId
     * @param {{lat: number, lng: number}[]} points
     * @param {string} [color]
     */
    function loadTrack(mapId, teamId, points, color) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;
        if (!points || points.length < 2) return;

        const trackColor = color || _teamColor(teamId);

        // Bestehenden Track entfernen falls vorhanden
        if (_tracks[teamId]) {
            mapData.phoneLayer.removeLayer(_tracks[teamId].polyline);
        }

        const polyline = L.polyline(
            points.map(p => [p.lat, p.lng]),
            { color: trackColor, weight: 3, opacity: 0.7, dashArray: '3 8' }
        ).addTo(mapData.phoneLayer);

        _tracks[teamId] = { polyline, color: trackColor };
    }

    /**
     * Fügt einen einzelnen GPS-Punkt zum laufenden Telefon-Track hinzu.
     * @param {string} mapId
     * @param {string} teamId
     * @param {number} lat
     * @param {number} lng
     */
    function appendTrackPoint(mapId, teamId, lat, lng) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;

        if (!_tracks[teamId]) {
            const trackColor = _teamColor(teamId);
            const polyline = L.polyline([[lat, lng]], {
                color: trackColor, weight: 3, opacity: 0.7, dashArray: '3 8'
            }).addTo(mapData.phoneLayer);
            _tracks[teamId] = { polyline, color: trackColor };
        } else {
            _tracks[teamId].polyline.addLatLng([lat, lng]);
        }
    }

    /**
     * Entfernt den Telefon-Track eines Teams von der Karte.
     */
    function clearTrack(mapId, teamId) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;
        if (_tracks[teamId]) {
            mapData.phoneLayer.removeLayer(_tracks[teamId].polyline);
            delete _tracks[teamId];
        }
    }

    function toggleVisibility(mapId, visible) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;

        if (visible) {
            if (!mapData.map.hasLayer(mapData.phoneLayer)) {
                mapData.map.addLayer(mapData.phoneLayer);
            }
        } else {
            mapData.map.removeLayer(mapData.phoneLayer);
        }
    }

    function removeMarker(mapId, teamId) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;
        if (_markers[teamId]) {
            mapData.phoneLayer.removeLayer(_markers[teamId]);
            delete _markers[teamId];
        }
    }

    function clearAll(mapId) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;
        mapData.phoneLayer.clearLayers();
        Object.keys(_markers).forEach(k => delete _markers[k]);
        Object.keys(_tracks).forEach(k => delete _tracks[k]);
    }

    return { initialize, updateMarker, loadTrack, appendTrackPoint, clearTrack, toggleVisibility, removeMarker, clearAll, setOptions };
})();
