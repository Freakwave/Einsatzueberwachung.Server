'use strict';

window.dashboardLayout = (function () {
    let _sortable = null;
    let _dotNetRef = null;
    let _containerId = 'monitor-dashboard-grid';

    function init(dotNetRef, containerId) {
        _dotNetRef = dotNetRef;
        _containerId = containerId || 'monitor-dashboard-grid';
        _destroySortable();
    }

    function enableEdit() {
        _destroySortable();
        const container = document.getElementById(_containerId);
        if (!container || typeof Sortable === 'undefined') return;

        _sortable = Sortable.create(container, {
            animation: 150,
            handle: '.dashboard-panel-drag-handle',
            ghostClass: 'dashboard-panel-ghost',
            chosenClass: 'dashboard-panel-chosen',
            dragClass: 'dashboard-panel-dragging',
            forceFallback: false,
            onEnd: function (evt) {
                if (_dotNetRef) {
                    _dotNetRef.invokeMethodAsync('OnPanelReordered', evt.oldIndex, evt.newIndex)
                        .catch(function () { });
                }
            }
        });

        container.classList.add('dashboard-edit-mode');
    }

    function disableEdit() {
        _destroySortable();
        const container = document.getElementById(_containerId);
        if (container) container.classList.remove('dashboard-edit-mode');
    }

    function _destroySortable() {
        if (_sortable) {
            _sortable.destroy();
            _sortable = null;
        }
    }

    function destroy() {
        _destroySortable();
        _dotNetRef = null;
    }

    return { init, enableEdit, disableEdit, destroy };
})();
