window.themeSync = (() => {
    const storageKey = "einsatz.theme";
    const legacyStorageKey = "theme";
    let dotNetRef = null;
    let storageHandler = null;
    let systemMediaQuery = null;
    let systemThemeHandler = null;

    function applyTheme(isDark) {
        const value = isDark ? "dark" : "light";
        document.documentElement.setAttribute("data-bs-theme", value);
        if (document.body) {
            document.body.setAttribute("data-bs-theme", value);
        }
    }

    function emitThemeChanged(isDark) {
        window.dispatchEvent(new CustomEvent("einsatz:theme-changed", {
            detail: {
                isDark: Boolean(isDark)
            }
        }));
    }

    function parseStoredTheme(value) {
        if (typeof value === "boolean") {
            return value;
        }

        if (value === null || value === undefined) {
            return false;
        }

        const normalized = String(value).trim().toLowerCase();
        return normalized === "dark"
            || normalized === "true"
            || normalized === "1"
            || normalized === "yes"
            || normalized === "on";
    }

    function getStoredTheme() {
        return localStorage.getItem(storageKey) || localStorage.getItem(legacyStorageKey);
    }

    function init(ref, initialIsDark) {
        dotNetRef = ref;

        const stored = getStoredTheme();
        const isDark = stored === null ? Boolean(initialIsDark) : parseStoredTheme(stored);

        applyTheme(isDark);
        localStorage.setItem(storageKey, isDark ? "dark" : "light");
        emitThemeChanged(isDark);

        storageHandler = (event) => {
            if ((event.key !== storageKey && event.key !== legacyStorageKey) || event.newValue === null) {
                return;
            }

            const changedIsDark = parseStoredTheme(event.newValue);
            applyTheme(changedIsDark);
            emitThemeChanged(changedIsDark);

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnThemeChangedFromStorage", changedIsDark);
            }
        };

        window.addEventListener("storage", storageHandler);
    }

    function setTheme(isDark) {
        applyTheme(isDark);
        localStorage.setItem(storageKey, isDark ? "dark" : "light");
        emitThemeChanged(isDark);

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnThemeChangedFromStorage", isDark);
        }
    }

    function stopWatchingSystemTheme() {
        if (systemMediaQuery && systemThemeHandler) {
            systemMediaQuery.removeEventListener("change", systemThemeHandler);
        }
        systemMediaQuery = null;
        systemThemeHandler = null;
    }

    function watchSystemTheme() {
        stopWatchingSystemTheme();

        systemMediaQuery = window.matchMedia("(prefers-color-scheme: dark)");

        // Apply immediately based on current OS preference
        const isDark = systemMediaQuery.matches;
        applyTheme(isDark);
        localStorage.setItem(storageKey, isDark ? "dark" : "light");
        emitThemeChanged(isDark);

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnThemeChangedFromStorage", isDark);
        }

        // Listen for future OS preference changes
        systemThemeHandler = (event) => {
            const changed = event.matches;
            applyTheme(changed);
            localStorage.setItem(storageKey, changed ? "dark" : "light");
            emitThemeChanged(changed);

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync("OnThemeChangedFromStorage", changed);
            }
        };

        systemMediaQuery.addEventListener("change", systemThemeHandler);
    }

    function dispose() {
        if (storageHandler) {
            window.removeEventListener("storage", storageHandler);
            storageHandler = null;
        }

        stopWatchingSystemTheme();
        dotNetRef = null;
    }

    return {
        init,
        setTheme,
        watchSystemTheme,
        stopWatchingSystemTheme,
        dispose
    };
})();
