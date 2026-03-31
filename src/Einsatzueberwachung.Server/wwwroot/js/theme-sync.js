window.themeSync = (() => {
    const storageKey = "einsatz.theme";
    let dotNetRef = null;
    let storageHandler = null;

    function applyTheme(isDark) {
        const value = isDark ? "dark" : "light";
        document.documentElement.setAttribute("data-bs-theme", value);
    }

    function parseStoredTheme(value) {
        return value === "dark";
    }

    function init(ref, initialIsDark) {
        dotNetRef = ref;

        const stored = localStorage.getItem(storageKey);
        const isDark = stored === null ? initialIsDark : parseStoredTheme(stored);

        applyTheme(isDark);
        localStorage.setItem(storageKey, isDark ? "dark" : "light");

        storageHandler = (event) => {
            if (event.key !== storageKey || event.newValue === null) {
                return;
            }

            const changedIsDark = parseStoredTheme(event.newValue);
            applyTheme(changedIsDark);

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnThemeChangedFromStorage", changedIsDark);
            }
        };

        window.addEventListener("storage", storageHandler);
    }

    function setTheme(isDark) {
        applyTheme(isDark);
        localStorage.setItem(storageKey, isDark ? "dark" : "light");
    }

    function dispose() {
        if (storageHandler) {
            window.removeEventListener("storage", storageHandler);
            storageHandler = null;
        }

        dotNetRef = null;
    }

    return {
        init,
        setTheme,
        dispose
    };
})();
