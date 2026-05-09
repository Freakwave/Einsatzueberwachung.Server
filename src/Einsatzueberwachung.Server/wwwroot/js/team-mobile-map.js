window.teamMobileMap = (function () {
    let map = null;
    let polygonLayer = null;
    let dogMarker = null;
    let trackLine = null;
    let userMarker = null;
    let userTrackLine = null;

    // Konfigurierbare Marker-Symbole
    let _collarIcon = 'paw';
    let _humanIcon = 'phone';

    function setOptions(opts) {
        if (opts && opts.collarIcon) _collarIcon = opts.collarIcon;
        if (opts && opts.humanIcon)  _humanIcon  = opts.humanIcon;
    }

    function _getCollarContent() {
        switch (_collarIcon) {
            case 'dog':  return '🐕';
            case 'bone': return '🦴';
            case 'dot':  return '<span style="width:10px;height:10px;background:#fff;border-radius:50%;display:block;"></span>';
            default:     return '🐾';
        }
    }

    function _getHumanContent() {
        switch (_humanIcon) {
            case 'person':         return '<i class="bi bi-person-fill"></i>';
            case 'person_walking': return '<i class="bi bi-person-walking"></i>';
            case 'radio':          return '<i class="bi bi-broadcast"></i>';
            case 'dot':            return '<span style="width:10px;height:10px;background:#fff;border-radius:50%;display:block;"></span>';
            default:               return '<i class="bi bi-phone-fill"></i>';
        }
    }

    function init(containerId, fallbackLat, fallbackLng) {
        if (map) return;
        const el = document.getElementById(containerId);
        if (el && el.clientHeight < 100) {
            // Fallback: explizite Hoehe falls Flex-Layout (CSS) noch nicht geladen ist.
            el.style.height = '60vh';
        }
        const center = [fallbackLat || 51.1657, fallbackLng || 10.4515];
        map = L.map(containerId, { zoomControl: true }).setView(center, 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap',
            maxZoom: 19
        }).addTo(map);
        // Falls der Container danach noch wachst (z.B. Header rendert), Leaflet neu vermessen.
        setTimeout(() => { try { map.invalidateSize(); } catch (e) { /* ignore */ } }, 250);
        setTimeout(() => { try { map.invalidateSize(); } catch (e) { /* ignore */ } }, 1500);
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
                html: `<div style="background:#dc3545;border:2px solid #fff;border-radius:50%;width:30px;height:30px;box-shadow:0 0 4px rgba(0,0,0,0.5);display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px;line-height:1;">${_getCollarContent()}</div>`,
                iconSize: [30, 30],
                iconAnchor: [15, 15]
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
                html: `<div style="background:#0d6efd;border:2px solid #fff;border-radius:50%;width:28px;height:28px;box-shadow:0 0 4px rgba(0,0,0,0.5);display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px;line-height:1;">${_getHumanContent()}</div>`,
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            });
            userMarker = L.marker(pos, { icon }).addTo(map);
        } else {
            userMarker.setLatLng(pos);
        }
    }

    function appendUserTrackPoint(lat, lng) {
        if (!map) return;
        if (!userTrackLine) {
            userTrackLine = L.polyline([[lat, lng]], {
                color: '#0d6efd',
                weight: 3,
                opacity: 0.7,
                dashArray: '3 8'
            }).addTo(map);
        } else {
            userTrackLine.addLatLng([lat, lng]);
        }
    }

    /**
     * Stellt einen bestehenden eigenen GPS-Track aus dem Server-Verlauf wieder her.
     * Wird nach einem Seitenneuladen aufgerufen, damit der Weg nicht verloren geht.
     * @param {{lat: number, lng: number}[]} points
     */
    function loadUserTrack(points) {
        if (!map || !points || points.length < 2) return;
        if (userTrackLine) { map.removeLayer(userTrackLine); userTrackLine = null; }
        userTrackLine = L.polyline(
            points.map(p => [p.lat, p.lng]),
            { color: '#0d6efd', weight: 3, opacity: 0.7, dashArray: '3 8' }
        ).addTo(map);
    }

    function centerOnDog() {
        if (map && dogMarker) map.panTo(dogMarker.getLatLng());
    }

    function destroy() {
        stopWatchingUser();
        if (map) { map.remove(); map = null; }
        polygonLayer = null;
        dogMarker = null;
        trackLine = null;
        userMarker = null;
        userTrackLine = null;
    }

    let watchId = null;
    let dotNetRef = null;

    function startWatchingUser(ref) {
        if (!('geolocation' in navigator)) return false;
        dotNetRef = ref;
        if (watchId !== null) return true;
        watchId = navigator.geolocation.watchPosition(
            pos => {
                const lat = pos.coords.latitude;
                const lng = pos.coords.longitude;
                setUserPosition(lat, lng);
                appendUserTrackPoint(lat, lng);
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnUserLocation', lat, lng).catch(() => {});
                }
            },
            err => { console.warn('Geolocation error', err); },
            { enableHighAccuracy: true, maximumAge: 5000, timeout: 15000 }
        );
        return true;
    }

    function stopWatchingUser() {
        if (watchId !== null) {
            navigator.geolocation.clearWatch(watchId);
            watchId = null;
        }
        dotNetRef = null;
    }

    function getAreaCentroid() {
        if (!polygonLayer) return null;
        try {
            const c = polygonLayer.getBounds().getCenter();
            return { lat: c.lat, lng: c.lng };
        } catch (e) { return null; }
    }

    function postLocation(lat, lng) {
        fetch('/api/team-mobile/location', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ lat, lng })
        }).catch(() => {});
    }

    return {
        init, renderSearchArea, setDogPosition, setTrack, appendTrackPoint,
        setUserPosition, appendUserTrackPoint, loadUserTrack, centerOnDog, destroy,
        startWatchingUser, stopWatchingUser, getAreaCentroid, postLocation, setOptions
    };
})();
