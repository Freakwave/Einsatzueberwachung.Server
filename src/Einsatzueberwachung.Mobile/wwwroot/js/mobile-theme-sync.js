window.mobileThemeSync = (() => {
    const storageKey = "einsatz.theme";

    function apply(value) {
        const normalized = value === "dark" ? "dark" : "light";
        document.documentElement.setAttribute("data-bs-theme", normalized);
    }

    function init() {
        const stored = localStorage.getItem(storageKey);
        apply(stored || "light");

        window.addEventListener("storage", (event) => {
            if (event.key !== storageKey || !event.newValue) {
                return;
            }

            apply(event.newValue);
        });
    }

    return {
        init
    };
})();
