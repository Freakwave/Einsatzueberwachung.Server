// Live-Uhr Initialisierung und Update
function initializeClock() {
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
    setInterval(updateClock, 1000);
}
