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
    _audioCtx: null,

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
    },

    // Web-Audio Beep (für akustischen Alarm bei Eskalation)
    beep(durationMs, frequency, volume) {
        durationMs = durationMs || 220;
        frequency = frequency || 880;
        volume = (typeof volume === 'number') ? volume : 0.25;
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;
            this._audioCtx = this._audioCtx || new Ctx();
            const ctx = this._audioCtx;
            // iOS / Safari: Ctx ist nach erstem User-Gesture suspended
            if (ctx.state === 'suspended') { ctx.resume().catch(() => {}); }
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.value = frequency;
            const t = ctx.currentTime;
            gain.gain.setValueAtTime(0, t);
            gain.gain.linearRampToValueAtTime(volume, t + 0.02);
            gain.gain.linearRampToValueAtTime(0, t + durationMs / 1000);
            osc.connect(gain).connect(ctx.destination);
            osc.start(t);
            osc.stop(t + durationMs / 1000);
        } catch (e) { /* still silence */ }
    },

    // Zweiton-Alarm bei Übergang in Eskalation
    alertSound() {
        this.beep(180, 880, 0.3);
        setTimeout(() => this.beep(180, 1175, 0.3), 220);
    },

    // localStorage-Wrapper (Toggles für Akustik, Lagezentrum-Modus etc.)
    getPref(key, defaultVal) {
        try {
            const v = localStorage.getItem('el-' + key);
            return v === null ? defaultVal : JSON.parse(v);
        } catch (e) { return defaultVal; }
    },

    setPref(key, value) {
        try { localStorage.setItem('el-' + key, JSON.stringify(value)); }
        catch (e) { /* ignore */ }
    }
};
