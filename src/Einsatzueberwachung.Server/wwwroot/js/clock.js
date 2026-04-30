// Live-Uhr Initialisierung und Update
let clockIntervalId = null;

function initializeClock() {
    if (clockIntervalId !== null) {
        return;
    }

    function updateClock() {
        const now = new Date();
        const timeEl = document.getElementById('live-time');
        const dateEl = document.getElementById('live-date');
        
        if (timeEl && dateEl) {
            // Format: HH:mm:ss
            const hours = String(now.getHours()).padStart(2, '0');
            const minutes = String(now.getMinutes()).padStart(2, '0');
            const seconds = String(now.getSeconds()).padStart(2, '0');
            timeEl.textContent = `${hours}:${minutes}:${seconds}`;
            
            // Format: dd.MM.yyyy
            const day = String(now.getDate()).padStart(2, '0');
            const month = String(now.getMonth() + 1).padStart(2, '0');
            const year = now.getFullYear();
            dateEl.textContent = `${day}.${month}.${year}`;
        }
    }
    
    // Initial update
    updateClock();
    
    // Update every second
    clockIntervalId = setInterval(updateClock, 1000);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeClock);
} else {
    initializeClock();
}

// EL-Dashboard Uhr (schreibt in beliebige Element-ID)
// Muss als window-Property deklariert sein, damit Blazor JS-Interop es findet
window.elDashboard = {
    _intervalId: null,
    startClock(elementId) {
        if (this._intervalId !== null) clearInterval(this._intervalId);
        const update = () => {
            const el = document.getElementById(elementId);
            if (!el) return;
            const now = new Date();
            const pad = n => String(n).padStart(2, '0');
            el.textContent = `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`;
        };
        update();
        this._intervalId = setInterval(update, 1000);
    }
};
