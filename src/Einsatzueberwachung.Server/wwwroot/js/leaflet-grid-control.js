// Leaflet Grid Selector Control
// Fuegt eine Radio-Button-Gruppe zum Layer-Control-Panel hinzu,
// um Koordinaten-Gitter (UTM / Lat/Lon) zu aktivieren/deaktivieren.

L.Control.GridSelector = L.Control.extend({

    options: {
        position: 'topright',
        collapsed: false
    },

    initialize: function (gridLayers, options) {
        L.setOptions(this, options);
        // gridLayers: { "Ohne": null, "UTM": utmLayerGroup, "Lat/Lon (dezimal)": latLonLayer }
        this._gridLayers = gridLayers;
        this._activeGrid = null;
    },

    onAdd: function (map) {
        this._map = map;

        var container = L.DomUtil.create('div', 'leaflet-control-grid-selector leaflet-control-layers');
        L.DomEvent.disableClickPropagation(container);
        L.DomEvent.disableScrollPropagation(container);

        var title = L.DomUtil.create('div', 'grid-selector-title', container);
        title.innerHTML = '<strong>Koordinaten-Gitter</strong>';

        var form = L.DomUtil.create('div', 'grid-selector-form', container);

        var self = this;
        var names = Object.keys(this._gridLayers);
        for (var i = 0; i < names.length; i++) {
            (function (name, layer) {
                var label = L.DomUtil.create('label', 'grid-selector-label', form);
                var radio = L.DomUtil.create('input', '', label);
                radio.type = 'radio';
                radio.name = 'grid-selector';
                radio.value = name;
                if (name === 'Ohne') {
                    radio.checked = true;
                }

                var span = L.DomUtil.create('span', '', label);
                span.textContent = ' ' + name;

                L.DomEvent.on(radio, 'change', function () {
                    self._switchGrid(name, layer);
                });
            })(names[i], this._gridLayers[names[i]]);
        }

        this._container = container;
        return container;
    },

    onRemove: function () {
        // Entferne aktives Gitter wenn Control entfernt wird
        if (this._activeGrid && this._map.hasLayer(this._activeGrid)) {
            this._map.removeLayer(this._activeGrid);
        }
        this._activeGrid = null;
    },

    _switchGrid: function (name, layer) {
        // Entferne aktuelles Gitter
        if (this._activeGrid && this._map.hasLayer(this._activeGrid)) {
            this._map.removeLayer(this._activeGrid);
        }
        this._activeGrid = null;

        // Fuege neues Gitter hinzu (falls nicht "Ohne")
        if (layer) {
            layer.addTo(this._map);
            this._activeGrid = layer;
        }
    }
});

L.control.gridSelector = function (gridLayers, options) {
    return new L.Control.GridSelector(gridLayers, options);
};
