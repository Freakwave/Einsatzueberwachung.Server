// Handy-GPS-Tracking: Zeigt Team-Telefon-Positionen auf der Karte als Kreise mit 2-Buchstaben-Kürzel

window.PhoneTracking = (function () {
    // teamId -> { marker }
    const _markers = {};

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
    }

    function _createIcon(abbrev, color) {
        return L.divIcon({
            className: '',
            html: `<div style="width:34px;height:34px;border-radius:50%;background:${color};border:3px solid #fff;box-shadow:0 1px 5px rgba(0,0,0,0.45);display:flex;align-items:center;justify-content:center;color:#fff;font-weight:700;font-size:12px;line-height:1;">${abbrev}</div>`,
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

    function updateMarker(mapId, teamId, teamName, lat, lng, timestamp) {
        const mapData = _getMapData(mapId);
        if (!mapData || !mapData.phoneLayer) return;

        const abbrev = (teamName || '??').substring(0, 2).toUpperCase();
        // Feste Farbe: deterministisch via Hashcode des teamId
        const colors = ['#1976d2', '#d32f2f', '#388e3c', '#f57c00', '#7b1fa2',
                        '#00838f', '#c2185b', '#5d4037', '#455a64', '#0288d1'];
        let h = 0;
        for (let i = 0; i < teamId.length; i++) h = (h * 31 + teamId.charCodeAt(i)) & 0xffff;
        const color = colors[h % colors.length];

        const popupContent = _buildPopup(teamName, lat, lng, timestamp);

        if (_markers[teamId]) {
            _markers[teamId].setLatLng([lat, lng]);
            _markers[teamId].setPopupContent(popupContent);
        } else {
            const icon = _createIcon(abbrev, color);
            const marker = L.marker([lat, lng], { icon })
                .bindPopup(popupContent);
            marker.addTo(mapData.phoneLayer);
            _markers[teamId] = marker;
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
    }

    return { initialize, updateMarker, toggleVisibility, removeMarker, clearAll };
})();
