window.layoutTools = window.layoutTools || {};

window.layoutTools.toggleFullscreen = async function () {
    if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
        return;
    }

    await document.exitFullscreen();
};
