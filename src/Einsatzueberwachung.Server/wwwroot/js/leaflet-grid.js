// Leaflet MetricGrid Plugin
// Zeichnet UTM- und Lat/Lon-Koordinatengitter auf der Karte
// Erfordert proj4js

L.MetricGrid = L.Layer.extend({

    options: {
        proj4ProjDef: "must be provided",
        bounds: [[0, 0], [0, 0]],
        clip: null,
        latLonClipBounds: null,
        drawClip: false,
        hundredKmSquareFunc: function (e, n) { return ""; },

        showAxisLabels: [100, 1000, 10000],
        showAxis100km: false,
        showSquareLabels: [],
        opacity: 0.7,
        weight: 2,
        color: "#00f",
        font: "bold 16px Verdana",
        density: 1,
        minInterval: 100,
        maxInterval: 100000,
        minZoom: 4
    },

    initialize: function (options) {
        L.setOptions(this, options);
        if (!this.options.fontColor) {
            this.options.fontColor = this.options.color;
        }
    },

    onAdd: function (map) {
        this._map = map;
        if (!this._container) {
            this._initCanvas();
        }
        map._panes.overlayPane.appendChild(this._container);
        map.on("viewreset", this._reset, this);
        map.on("move", this._reset, this);
        map.on("moveend", this._reset, this);
        this._reset();
    },

    onRemove: function (map) {
        map.getPanes().overlayPane.removeChild(this._container);
        map.off("viewreset", this._reset, this);
        map.off("move", this._reset, this);
        map.off("moveend", this._reset, this);
    },

    addTo: function (map) {
        map.addLayer(this);
        return this;
    },

    getAttribution: function () {
        return this.options.attribution;
    },

    setOpacity: function (opacity) {
        this.options.opacity = opacity;
        this._updateOpacity();
        return this;
    },

    bringToFront: function () {
        if (this._canvas) {
            this._map._panes.overlayPane.appendChild(this._canvas);
        }
        return this;
    },

    bringToBack: function () {
        var pane = this._map._panes.overlayPane;
        if (this._canvas) {
            pane.insertBefore(this._canvas, pane.firstChild);
        }
        return this;
    },

    _initCanvas: function () {
        this._container = L.DomUtil.create("div", "leaflet-image-layer");
        this._canvas = L.DomUtil.create("canvas", "");
        this._updateOpacity();
        this._container.appendChild(this._canvas);
        L.extend(this._canvas, {
            onselectstart: L.Util.falseFn,
            onmousemove: L.Util.falseFn,
            onload: L.bind(this._onCanvasLoad, this)
        });
    },

    _setClip: function (ctx) {
        var map = this._map;
        var proj = this.options.proj4ProjDef;
        var i;

        if (this.options.clip) {
            var x2, y2, x1, y1, dX, dY, pts, j;

            for (i = 0; i < (this.options.clip.length - 1); i += 1) {
                x2 = this.options.clip[i + 1][0];
                x1 = this.options.clip[i][0];
                y2 = this.options.clip[i + 1][1];
                y1 = this.options.clip[i][1];
                dX = x2 - x1;
                dY = y2 - y1;

                var _x1 = x1, _y1 = y1, _dX = dX, _dY = dY;
                function _interpolate(frac) {
                    return proj4(proj).inverse([_x1 + (frac * _dX), _y1 + (frac * _dY)]);
                }

                pts = this._getPoints(_interpolate, 1.0, map);

                j = 0;
                if (i === 0) {
                    ctx.beginPath();
                    ctx.moveTo(pts[0].x, pts[0].y);
                    j = 1;
                }
                for (j = j; j < pts.length; j += 1) {
                    ctx.lineTo(pts[j].x, pts[j].y);
                }
            }

            if (this.options.drawClip) {
                ctx.stroke();
            }
            ctx.clip();
        }
    },

    _setLLClipBounds: function (ctx, map) {
        var b = L.latLngBounds(this.options.latLonClipBounds);
        var bl = map.latLngToContainerPoint(b.getSouthWest());
        var tr = map.latLngToContainerPoint(b.getNorthEast());

        ctx.beginPath();
        ctx.moveTo(bl.x, bl.y);
        ctx.lineTo(tr.x, bl.y);
        ctx.lineTo(tr.x, tr.y);
        ctx.lineTo(bl.x, tr.y);
        ctx.lineTo(bl.x, bl.y);

        if (this.options.drawClip) {
            ctx.stroke();
        }
        ctx.clip();

        return L.bounds(bl, tr);
    },

    _reset: function () {
        var container = this._container;
        var canvas = this._canvas;
        var size = this._map.getSize();
        var lt = this._map.containerPointToLayerPoint([0, 0]);

        L.DomUtil.setPosition(container, lt);

        container.style.width = size.x + "px";
        container.style.height = size.y + "px";

        canvas.width = size.x;
        canvas.height = size.y;
        canvas.style.width = size.x + "px";
        canvas.style.height = size.y + "px";

        this._draw();
    },

    _onCanvasLoad: function () {
        this.fire("load");
    },

    _updateOpacity: function () {
        L.DomUtil.setOpacity(this._canvas, this.options.opacity);
    },

    _formatEastOrNorth: function (n, spacing) {
        var r;
        var h = Math.floor(n / 100000);
        n = n % 100000;

        if (spacing < 1000) {
            r = Math.floor(n / 100).toString();
            r = (r.length === 1) ? "0" + r : r;
            r = (r.length === 2) ? "0" + r : r;
        } else if (spacing < 10000) {
            r = Math.floor(n / 1000).toString();
            r = (r.length === 1) ? "0" + r : r;
        } else {
            r = Math.floor(n / 10000).toString();
        }

        if (this.options.showAxis100km) {
            var hs = h.toString();
            var i;
            for (i = (hs.length - 1); i >= 0; i--) {
                r = String.fromCharCode(hs.charCodeAt(i) + 8272) + r;
            }
        }

        return r;
    },

    _format_eastings: function (eastings, spacing) {
        return this._formatEastOrNorth(eastings, spacing);
    },

    _format_northings: function (northings, spacing) {
        return this._formatEastOrNorth(northings, spacing);
    },

    _mPerPx: function () {
        var ll1 = this._map.getCenter();
        var p1 = this._map.project(ll1);
        var p2 = p1.add(new L.Point(1, 0));
        var ll2 = this._map.unproject(p2);
        return ll1.distanceTo(ll2);
    },

    _calcInterval: function () {
        var mPerPx = this._mPerPx();
        var spacing;
        if (mPerPx <= 1) {
            spacing = 100;
        } else if (mPerPx <= 20) {
            spacing = 1000;
        } else if (mPerPx <= 175) {
            spacing = 10000;
        } else {
            spacing = 100000;
        }

        if (this.options.density)
            spacing = spacing / this.options.density;

        if (spacing < this.options.minInterval) {
            spacing = this.options.minInterval;
        }
        if (spacing > this.options.maxInterval) {
            spacing = this.options.maxInterval;
        }

        return spacing;
    },

    _getPoints: function (interpolate, tolerance, map) {
        var geoA = interpolate(0);
        var geoB = interpolate(1);

        var a = map.latLngToContainerPoint(L.latLng(geoA[1], geoA[0]));
        var b = map.latLngToContainerPoint(L.latLng(geoB[1], geoB[0]));

        var coords = [];
        var geoStack = [geoB, geoA];
        var stack = [b, a];
        var fractionStack = [1, 0];
        var fractions = {};
        var maxIterations = 1000;
        var geoM, m, fracA, fracB, fracM, key;

        while (--maxIterations > 0 && fractionStack.length > 0) {
            fracA = fractionStack.pop();
            geoA = geoStack.pop();
            a = stack.pop();

            key = fracA.toString();
            if (!fractions[key]) {
                coords.push(a);
                fractions[key] = true;
            }

            fracB = fractionStack.pop();
            geoB = geoStack.pop();
            b = stack.pop();

            fracM = (fracA + fracB) / 2;
            geoM = interpolate(fracM);
            m = map.latLngToContainerPoint(L.latLng(geoM[1], geoM[0]));

            if (L.LineUtil.pointToSegmentDistance(m, a, b) < tolerance) {
                coords.push(b);
                key = fracB.toString();
                fractions[key] = true;
            } else {
                fractionStack.push(fracB, fracM, fracM, fracA);
                stack.push(b, m, m, a);
                geoStack.push(geoB, geoM, geoM, geoA);
            }
        }
        return coords;
    },

    _inside: function (point, vs) {
        var x = point[0];
        var y = point[1];
        var i, j;

        var inside = false;
        for (i = 0, j = vs.length - 1; i < vs.length; j = i++) {
            var xi = vs[i][0], yi = vs[i][1];
            var xj = vs[j][0], yj = vs[j][1];

            var intersect = ((yi > y) !== (yj > y))
                && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }

        return inside;
    },

    _draw: function () {
        var canvas = this._canvas;
        var map = this._map;

        if (L.Browser.canvas && map) {
            var zoom = map.getZoom();
            if (this.options.minZoom && zoom < this.options.minZoom)
                return;
            if (this.options.maxZoom && zoom > this.options.maxZoom)
                return;
            if (this.options.skipZoom && this.options.skipZoom.indexOf(zoom) > -1)
                return;

            var spacing = this._calcInterval();
            var proj = this.options.proj4ProjDef;
            var ctx = canvas.getContext("2d");

            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.lineWidth = this.options.weight;
            ctx.strokeStyle = this.options.color;
            ctx.fillStyle = this.options.fontColor;

            if (this.options.font) {
                ctx.font = this.options.font;
            }
            var txtWidth = ctx.measureText("0").width;
            var txtHeight;
            var _font_frags = ctx.font.split(" ");
            var i;
            for (i = 0; i < _font_frags.length; i += 1) {
                txtHeight = parseInt(_font_frags[i], 10);
                if (!isNaN(txtHeight)) {
                    break;
                }
            }

            var mapB = map.getBounds();
            var mapSW = mapB.getSouthWest();
            var mapNE = mapB.getNorthEast();
            var mapNW = mapB.getNorthWest();
            var mapSE = mapB.getSouthEast();
            var mapSWg = proj4(proj).forward([mapSW.lng, mapSW.lat]);
            var mapNEg = proj4(proj).forward([mapNE.lng, mapNE.lat]);
            var mapNWg = proj4(proj).forward([mapNW.lng, mapNW.lat]);
            var mapSEg = proj4(proj).forward([mapSE.lng, mapSE.lat]);

            var mapSMg = proj4(proj).forward([mapB.getCenter().lng, mapB.getSouth()]);
            var mapNMg = proj4(proj).forward([mapB.getCenter().lng, mapB.getNorth()]);
            var mapWMg = proj4(proj).forward([mapB.getWest(), mapB.getCenter().lat]);
            var mapEMg = proj4(proj).forward([mapB.getEast(), mapB.getCenter().lat]);

            var grdWx = Math.min(mapSWg[0], mapNWg[0]);
            var grdEx = Math.max(mapSEg[0], mapNEg[0]);
            var grdSy = Math.min(mapSWg[1], mapSEg[1]);
            var grdNy = Math.max(mapNWg[1], mapNEg[1]);

            grdWx = Math.min(mapWMg[0], grdWx);
            grdEx = Math.max(mapEMg[0], grdEx);
            grdSy = Math.min(mapSMg[1], grdSy);
            grdNy = Math.max(mapNMg[1], grdNy);

            grdWx = Math.floor(grdWx / spacing) * spacing;
            grdSy = Math.floor(grdSy / spacing) * spacing;
            grdEx = Math.ceil(grdEx / spacing) * spacing;
            grdNy = Math.ceil(grdNy / spacing) * spacing;

            var canvasClipBounds = null;
            var hasLLClip = false;
            if (this.options.clip) {
                var swInClip = this._inside([grdWx, grdSy], this.options.clip);
                var seInClip = this._inside([grdEx, grdSy], this.options.clip);
                var neInClip = this._inside([grdEx, grdNy], this.options.clip);
                var nwInClip = this._inside([grdWx, grdNy], this.options.clip);

                if ((!swInClip) || (!seInClip) || (!neInClip) || (!nwInClip)) {
                    this._setClip(ctx);
                }
            } else if (this.options.latLonClipBounds) {
                hasLLClip = true;
                ctx.save();
                canvasClipBounds = this._setLLClipBounds(ctx, map);
            }

            if (grdWx < this.options.bounds[0][0]) {
                grdWx = Math.floor(this.options.bounds[0][0] / spacing) * spacing;
            }
            if (grdWx > this.options.bounds[1][0]) {
                if (hasLLClip) ctx.restore();
                return;
            }
            if (grdEx > this.options.bounds[1][0]) {
                grdEx = Math.ceil(this.options.bounds[1][0] / spacing) * spacing;
            }
            if (grdEx < this.options.bounds[0][0]) {
                if (hasLLClip) ctx.restore();
                return;
            }
            if (grdSy < this.options.bounds[0][1]) {
                grdSy = Math.floor(this.options.bounds[0][1] / spacing) * spacing;
            }
            if (grdSy > this.options.bounds[1][1]) {
                if (hasLLClip) ctx.restore();
                return;
            }
            if (grdNy > this.options.bounds[1][1]) {
                grdNy = Math.ceil(this.options.bounds[1][1] / spacing) * spacing;
            }
            if (grdNy < this.options.bounds[0][1]) {
                if (hasLLClip) ctx.restore();
                return;
            }

            var ww = canvas.width;
            var hh = canvas.height;

            var d = spacing;
            var d2 = d / 2;

            // Verticals of constant Eastings
            var h = grdNy - grdSy;
            for (var x = grdWx; x <= grdEx; x += d) {
                (function (xVal) {
                    function _interpolateY(frac) {
                        return proj4(proj).inverse([xVal, grdNy - (frac * h)]);
                    }
                    var pts = this._getPoints(_interpolateY, 1.0, map);
                    ctx.beginPath();
                    ctx.moveTo(pts[0].x, pts[0].y);
                    for (var ii = 1; ii < pts.length; ii++) {
                        ctx.lineTo(pts[ii].x, pts[ii].y);
                    }
                    ctx.stroke();
                }).call(this, x);
            }

            // Horizontals of constant Northings
            var w = grdEx - grdWx;
            for (var y = grdSy; y <= grdNy; y += d) {
                (function (yVal) {
                    function _interpolateX(frac) {
                        return proj4(proj).inverse([grdEx - (frac * w), yVal]);
                    }
                    var pts = this._getPoints(_interpolateX, 1.0, map);
                    ctx.beginPath();
                    ctx.moveTo(pts[0].x, pts[0].y);
                    for (var ii = 1; ii < pts.length; ii++) {
                        ctx.lineTo(pts[ii].x, pts[ii].y);
                    }
                    ctx.stroke();
                }).call(this, y);
            }

            // Restore canvas state to remove clip before drawing labels
            if (hasLLClip) {
                ctx.restore();
                // Re-apply styles after restore
                ctx.lineWidth = this.options.weight;
                ctx.strokeStyle = this.options.color;
                if (this.options.font) {
                    ctx.font = this.options.font;
                }
            }

            // Axis labels
            ctx.fillStyle = this.options.color;
            var rubWidth = this.options.weight * 3;

            // Eastings axis labels
            if (this.options.showAxisLabels.indexOf(d) >= 0) {
                for (var x = grdWx; x <= grdEx; x += d) {
                    for (var y = grdSy; y <= grdNy; y += d) {
                        var ll = proj4(proj).inverse([x, y + d2]);
                        var s = map.latLngToContainerPoint(L.latLng(ll[1], ll[0]));

                        if ((s.x > 0) && (s.y < hh) && (x < this.options.bounds[1][0])) {
                            if (this.options.clip) {
                                if (!this._inside([x, y + d2], this.options.clip)) {
                                    continue;
                                }
                            } else if (this.options.latLonClipBounds) {
                                if (!canvasClipBounds.contains([s.x, s.y])) {
                                    continue;
                                }
                            }

                            var eStr = this._format_eastings(x, d);
                            txtWidth = ctx.measureText(eStr).width;

                            ctx.globalCompositeOperation = "destination-out";
                            ctx.fillRect(s.x - (rubWidth / 2), s.y - txtHeight, rubWidth, txtHeight * 1.2);
                            ctx.globalCompositeOperation = "source-over";

                            ctx.fillText(eStr, s.x - (txtWidth / 2), s.y);
                            break;
                        }
                    }
                }
            }

            // Northings axis labels
            if (this.options.showAxisLabels.indexOf(d) >= 0) {
                for (var y = grdSy; y <= grdNy; y += d) {
                    for (var x = grdWx; x <= grdEx; x += d) {
                        var ll = proj4(proj).inverse([x + d2, y]);
                        var s = map.latLngToContainerPoint(L.latLng(ll[1], ll[0]));

                        if ((s.x > 0) && (s.y < hh) && (y < this.options.bounds[1][1])) {
                            if (this.options.clip) {
                                if (!this._inside([x + d2, y], this.options.clip)) {
                                    continue;
                                }
                            } else if (this.options.latLonClipBounds) {
                                if (!canvasClipBounds.contains([s.x, s.y])) {
                                    continue;
                                }
                            }

                            var nStr = this._format_northings(y, d);
                            txtWidth = ctx.measureText(nStr).width;

                            ctx.globalCompositeOperation = "destination-out";
                            ctx.fillRect(s.x - txtWidth * 0.1, s.y - (rubWidth / 2), txtWidth * 1.2, rubWidth);
                            ctx.globalCompositeOperation = "source-over";

                            ctx.fillText(nStr, s.x, s.y + (txtHeight / 2));
                            break;
                        }
                    }
                }
            }

            // Grid Square labels
            var str;
            if (this.options.showSquareLabels.indexOf(d) >= 0) {
                for (var y = grdSy; y <= grdNy; y += d) {
                    for (var x = grdWx; x <= grdEx; x += d) {
                        var ll = proj4(proj).inverse([x, y]);
                        var s = map.latLngToContainerPoint(L.latLng(ll[1], ll[0]));

                        if ((s.x > 0) && (s.y < hh) && (x < this.options.bounds[1][0]) && (y < this.options.bounds[1][1])) {
                            var nStr = this._format_northings(y, d);
                            var eStr = this._format_eastings(x, d);
                            var sq = this.options.hundredKmSquareFunc(x, y);
                            str = sq;
                            if (d < 100000) {
                                str += eStr + nStr;
                            }
                            ctx.fillText(str, s.x + 2, s.y - 2);
                        }
                    }
                }
            }
        }
    }
});

L.metricGrid = function (options) {
    return new L.MetricGrid(options);
};

/** UTM Grid */
L.UtmGrid = L.MetricGrid.extend({

    options: {
        bounds: [[100000, 0], [900000, 9400000]]
    },

    initialize: function (zone, bSouth, options) {
        options = options || {};
        options.proj4ProjDef = "+proj=utm +zone=" + zone + " +ellps=WGS84 +datum=WGS84 +units=m +no_defs";
        if (bSouth) {
            options.proj4ProjDef += " +south";
            options.bounds = [[100000, 600000], [900000, 10000000]];
        }

        options.hundredKmSquareFunc = function (e, n) {
            var r = "";
            var UTMEast = [
                "ABCDEFGH",
                "JKLMNPQR",
                "STUVWXYZ"
            ];

            var UTMNorthGroup1 = [
                "ABCDEFGHJKLMNPQRSTUV",
                "FGHJKLMNPQRSTUVABCDE"
            ];

            var x = Math.floor(e / 100000);
            var y = Math.floor(n / 100000);
            var z = zone - 1;

            if (bSouth) {
                y -= 100;
            }

            if ((x >= 1) && (x <= 8)) {
                r = UTMEast[z % 3].charAt(x - 1);
            } else {
                r = '-';
            }

            if (y >= 0) {
                r += UTMNorthGroup1[z % 2].charAt(y % 20);
            } else {
                r += UTMNorthGroup1[z % 2].charAt(19 + ((y + 1) % 20));
            }
            return r;
        };

        L.setOptions(this, options);
    }
});

L.utmGrid = function (zone, bSouth, options) {
    return new L.UtmGrid(zone, bSouth, options);
};


/**
 * Lat/Lon Dezimal-Gitter
 * Zeichnet ein einfaches Gitter mit geografischen Koordinaten (WGS84)
 * Nutzt eine Plate Carree-Projektion (Identitaet: forward/inverse = Identitaet fuer lon/lat)
 */
L.LatLonGrid = L.Layer.extend({

    options: {
        opacity: 0.7,
        weight: 1.5,
        color: "#d63384",
        fontColor: null,
        font: "bold 12px Verdana",
        minZoom: 3
    },

    initialize: function (options) {
        L.setOptions(this, options);
        if (!this.options.fontColor) {
            this.options.fontColor = this.options.color;
        }
    },

    onAdd: function (map) {
        this._map = map;
        if (!this._container) {
            this._initCanvas();
        }
        map._panes.overlayPane.appendChild(this._container);
        map.on("viewreset", this._reset, this);
        map.on("move", this._reset, this);
        map.on("moveend", this._reset, this);
        this._reset();
    },

    onRemove: function (map) {
        map.getPanes().overlayPane.removeChild(this._container);
        map.off("viewreset", this._reset, this);
        map.off("move", this._reset, this);
        map.off("moveend", this._reset, this);
    },

    addTo: function (map) {
        map.addLayer(this);
        return this;
    },

    _initCanvas: function () {
        this._container = L.DomUtil.create("div", "leaflet-image-layer");
        this._canvas = L.DomUtil.create("canvas", "");
        L.DomUtil.setOpacity(this._canvas, this.options.opacity);
        this._container.appendChild(this._canvas);
        L.extend(this._canvas, {
            onselectstart: L.Util.falseFn,
            onmousemove: L.Util.falseFn
        });
    },

    _reset: function () {
        var size = this._map.getSize();
        var lt = this._map.containerPointToLayerPoint([0, 0]);

        L.DomUtil.setPosition(this._container, lt);
        this._container.style.width = size.x + "px";
        this._container.style.height = size.y + "px";
        this._canvas.width = size.x;
        this._canvas.height = size.y;
        this._canvas.style.width = size.x + "px";
        this._canvas.style.height = size.y + "px";

        this._draw();
    },

    _calcInterval: function () {
        var zoom = this._map.getZoom();
        // Gitter-Intervall je nach Zoom-Stufe (in Dezimalgrad)
        if (zoom >= 18) return 0.0005;
        if (zoom >= 16) return 0.001;
        if (zoom >= 14) return 0.005;
        if (zoom >= 12) return 0.01;
        if (zoom >= 10) return 0.05;
        if (zoom >= 8) return 0.1;
        if (zoom >= 6) return 0.5;
        if (zoom >= 4) return 1;
        if (zoom >= 2) return 5;
        return 10;
    },

    _formatCoord: function (val, isLat) {
        var abs = Math.abs(val);
        var suffix = isLat ? (val >= 0 ? "N" : "S") : (val >= 0 ? "E" : "W");
        // Format-Praezision abhaengig vom Intervall
        var interval = this._calcInterval();
        var decimals = 0;
        if (interval < 0.001) decimals = 4;
        else if (interval < 0.01) decimals = 3;
        else if (interval < 0.1) decimals = 2;
        else if (interval < 1) decimals = 1;
        return abs.toFixed(decimals) + "\u00B0" + suffix;
    },

    _draw: function () {
        var map = this._map;
        var canvas = this._canvas;
        var ctx = canvas.getContext("2d");

        if (!map) return;

        var zoom = map.getZoom();
        if (this.options.minZoom && zoom < this.options.minZoom) return;

        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.lineWidth = this.options.weight;
        ctx.strokeStyle = this.options.color;
        ctx.fillStyle = this.options.fontColor;
        if (this.options.font) {
            ctx.font = this.options.font;
        }

        var bounds = map.getBounds();
        var interval = this._calcInterval();

        var south = Math.floor(bounds.getSouth() / interval) * interval;
        var north = Math.ceil(bounds.getNorth() / interval) * interval;
        var west = Math.floor(bounds.getWest() / interval) * interval;
        var east = Math.ceil(bounds.getEast() / interval) * interval;

        var txtHeight = 12;
        var _font_frags = ctx.font.split(" ");
        for (var fi = 0; fi < _font_frags.length; fi++) {
            var parsed = parseInt(_font_frags[fi], 10);
            if (!isNaN(parsed)) { txtHeight = parsed; break; }
        }

        // Vertikale Linien (Laengengrade)
        for (var lng = west; lng <= east; lng += interval) {
            // Auf Intervall runden um Floating-Point-Artefakte zu vermeiden
            lng = Math.round(lng / interval) * interval;
            var topPt = map.latLngToContainerPoint(L.latLng(bounds.getNorth(), lng));
            var botPt = map.latLngToContainerPoint(L.latLng(bounds.getSouth(), lng));
            ctx.beginPath();
            ctx.moveTo(topPt.x, topPt.y);
            ctx.lineTo(botPt.x, botPt.y);
            ctx.stroke();

            // Label oben
            var lbl = this._formatCoord(lng, false);
            var tw = ctx.measureText(lbl).width;
            ctx.fillText(lbl, topPt.x - tw / 2, topPt.y + txtHeight + 4);
        }

        // Horizontale Linien (Breitengrade)
        for (var lat = south; lat <= north; lat += interval) {
            lat = Math.round(lat / interval) * interval;
            var leftPt = map.latLngToContainerPoint(L.latLng(lat, bounds.getWest()));
            var rightPt = map.latLngToContainerPoint(L.latLng(lat, bounds.getEast()));
            ctx.beginPath();
            ctx.moveTo(leftPt.x, leftPt.y);
            ctx.lineTo(rightPt.x, rightPt.y);
            ctx.stroke();

            // Label links
            var lbl = this._formatCoord(lat, true);
            ctx.fillText(lbl, leftPt.x + 4, leftPt.y - 4);
        }
    }
});

L.latLonGrid = function (options) {
    return new L.LatLonGrid(options);
};
