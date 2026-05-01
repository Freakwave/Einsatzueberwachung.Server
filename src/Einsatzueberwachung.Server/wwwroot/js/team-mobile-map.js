window.teamMobileMap = (function () {
    let map = null;
    let polygonLayer = null;
    let dogMarker = null;
    let trackLine = null;
    let userMarker = null;

    function init(containerId, fallbackLat, fallbackLng) {
        if (map) return;
        const center = [fallbackLat || 51.1657, fallbackLng || 10.4515];
        map = L.map(containerId, { zoomControl: true }).setView(center, 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap',
            maxZoom: 19
        }).addTo(map);
    }

    function renderSearchArea(coords, color) {
        if (!map) return;
        if (polygonLayer) map.removeLayer(polygonLayer);
        if (!coords || coords.length < 3) return;
        polygonLayer = L.polygon(coords.map(c => [c.lat, c.lng]), {
            color: color || '#3388ff',
            weight: 3,
            fillOpacity: 0.15
        }).addTo(map);
        try { map.fitBounds(polygonLayer.getBounds(), { padding: [30, 30] }); } catch (e) { /* ignore */ }
    }

    function setDogPosition(lat, lng, dogName) {
        if (!map) return;
        const pos = [lat, lng];
        if (!dogMarker) {
            const icon = L.divIcon({
                className: 'team-mobile-dog-marker',
                html: '<div style="background:#dc3545;border:2px solid #fff;border-radius:50%;width:18px;height:18px;box-shadow:0 0 4px rgba(0,0,0,0.5);"></div>',
                iconSize: [18, 18],
                iconAnchor: [9, 9]
            });
            dogMarker = L.marker(pos, { icon }).addTo(map);
            if (dogName) dogMarker.bindTooltip(dogName, { permanent: false });
        } else {
            dogMarker.setLatLng(pos);
        }
    }

    function setTrack(points) {
        if (!map) return;
        if (trackLine) { map.removeLayer(trackLine); trackLine = null; }
        if (!points || points.length < 2) return;
        trackLine = L.polyline(points.map(p => [p.lat, p.lng]), {
            color: '#dc3545',
            weight: 3,
            opacity: 0.7
        }).addTo(map);
    }

    function appendTrackPoint(lat, lng) {
        if (!map) return;
        if (!trackLine) {
            trackLine = L.polyline([[lat, lng]], { color: '#dc3545', weight: 3, opacity: 0.7 }).addTo(map);
        } else {
            trackLine.addLatLng([lat, lng]);
        }
    }

    function setUserPosition(lat, lng) {
        if (!map) return;
        const pos = [lat, lng];
        if (!userMarker) {
            const icon = L.divIcon({
                className: 'team-mobile-user-marker',
                html: '<div style="background:#0d6efd;border:2px solid #fff;border-radius:50%;width:14px;height:14px;box-shadow:0 0 4px rgba(0,0,0,0.5);"></div>',
                iconSize: [14, 14],
                iconAnchor: [7, 7]
            });
            userMarker = L.marker(pos, { icon }).addTo(map);
        } else {
            userMarker.setLatLng(pos);
        }
    }

    function centerOnDog() {
        if (map && dogMarker) map.panTo(dogMarker.getLatLng());
    }

    function destroy() {
        if (map) { map.remove(); map = null; }
        polygonLayer = null;
        dogMarker = null;
        trackLine = null;
        userMarker = null;
    }

    return { init, renderSearchArea, setDogPosition, setTrack, appendTrackPoint, setUserPosition, centerOnDog, destroy };
})();
