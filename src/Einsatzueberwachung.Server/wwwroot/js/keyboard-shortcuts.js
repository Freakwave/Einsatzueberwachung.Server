(function () {
    let initialized = false;

    function navigate(path) {
        window.location.href = path;
    }

    function onKeyDown(event) {
        if (!event.ctrlKey || event.altKey || event.metaKey) {
            return;
        }

        const key = event.key.toLowerCase();

        switch (key) {
            case "h":
                event.preventDefault();
                navigate("/");
                break;
            case "m":
                event.preventDefault();
                navigate("/einsatz-karte");
                break;
            case "n":
                event.preventDefault();
                navigate("/einsatz-monitor");
                break;
            case "t":
                event.preventDefault();
                navigate("/einsatz-start");
                break;
            default:
                break;
        }
    }

    function init() {
        if (initialized) {
            return;
        }

        window.addEventListener("keydown", onKeyDown);
        initialized = true;
    }

    init();
})();
