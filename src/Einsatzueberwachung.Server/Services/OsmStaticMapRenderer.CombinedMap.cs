using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using SkiaSharp;

namespace Einsatzueberwachung.Server.Services;

public sealed partial class OsmStaticMapRenderer
{
    public async Task<byte[]?> RenderCombinedTrackMapAsync(
        List<TeamTrackSnapshot> tracks,
        (double Latitude, double Longitude)? elwPosition,
        int width = 1200,
        int height = 780)
    {
        var validTracks = tracks.Where(track => track.Points.Count >= 2).ToList();
        if (validTracks.Count == 0)
            return null;

        try
        {
            var allLats = validTracks.SelectMany(track => track.Points.Select(p => p.Latitude)).ToList();
            var allLons = validTracks.SelectMany(track => track.Points.Select(p => p.Longitude)).ToList();

            foreach (var areaPoint in validTracks
                .Where(track => track.SearchAreaCoordinates is { Count: >= 3 })
                .SelectMany(track => track.SearchAreaCoordinates))
            {
                allLats.Add(areaPoint.Latitude);
                allLons.Add(areaPoint.Longitude);
            }

            if (elwPosition.HasValue) { allLats.Add(elwPosition.Value.Latitude); allLons.Add(elwPosition.Value.Longitude); }

            var minLat = allLats.Min(); var maxLat = allLats.Max();
            var minLon = allLons.Min(); var maxLon = allLons.Max();

            var latPad = Math.Max((maxLat - minLat) * 0.10, 0.001);
            var lonPad = Math.Max((maxLon - minLon) * 0.10, 0.001);
            minLat -= latPad; maxLat += latPad;
            minLon -= lonPad; maxLon += lonPad;

            var zoom = CalculateZoom(minLat, maxLat, minLon, maxLon, width, height);

            var absCropLeft = LonToTileXFloat(minLon, zoom) * TileSize;
            var absCropTop = LatToTileYFloat(maxLat, zoom) * TileSize;
            var absCropRight = LonToTileXFloat(maxLon, zoom) * TileSize;
            var absCropBottom = LatToTileYFloat(minLat, zoom) * TileSize;

            AdjustCropToAspect(ref absCropLeft, ref absCropTop, ref absCropRight, ref absCropBottom, width, height);

            var minTileX = (int)Math.Floor(absCropLeft / TileSize);
            var maxTileX = (int)Math.Floor(absCropRight / TileSize);
            var minTileY = (int)Math.Floor(absCropTop / TileSize);
            var maxTileY = (int)Math.Floor(absCropBottom / TileSize);

            var tiles = await DownloadTilesAsync(minTileX, maxTileX, minTileY, maxTileY, zoom);
            var (fullBitmap, _, _) = AssembleTileMosaic(tiles, minTileX, minTileY, maxTileX, maxTileY, TileSize);

            using (fullBitmap)
            {
                var cropLeft = (float)(absCropLeft - minTileX * TileSize);
                var cropTop = (float)(absCropTop - minTileY * TileSize);
                var cropRight = (float)(absCropRight - minTileX * TileSize);
                var cropBottom = (float)(absCropBottom - minTileY * TileSize);

                using var outputBitmap = new SKBitmap(width, height);
                using var canvas = new SKCanvas(outputBitmap);
                canvas.DrawBitmap(fullBitmap, new SKRect(cropLeft, cropTop, cropRight, cropBottom), new SKRect(0, 0, width, height));

                var scaleX = width / (cropRight - cropLeft);
                var scaleY = height / (cropBottom - cropTop);
                float ToX(double lon) => ((float)((LonToTileXFloat(lon, zoom) - minTileX) * TileSize) - cropLeft) * scaleX;
                float ToY(double lat) => ((float)((LatToTileYFloat(lat, zoom) - minTileY) * TileSize) - cropTop) * scaleY;

                var renderedAreas = new HashSet<string>(StringComparer.Ordinal);
                foreach (var track in validTracks.Where(t => t.SearchAreaCoordinates is { Count: >= 3 }))
                {
                    var areaKey = string.Join('|', track.SearchAreaCoordinates.Select(c => $"{c.Latitude:F6},{c.Longitude:F6}"));
                    if (!renderedAreas.Add(areaKey)) continue;

                    var areaPoints = track.SearchAreaCoordinates.Select(c => new SKPoint(ToX(c.Longitude), ToY(c.Latitude))).ToArray();
                    var parsedAreaColor = ParseColor(string.IsNullOrWhiteSpace(track.SearchAreaColor) ? "#3388ff" : track.SearchAreaColor);

                    using var areaPath = new SKPath();
                    areaPath.MoveTo(areaPoints[0]);
                    for (var i = 1; i < areaPoints.Length; i++) areaPath.LineTo(areaPoints[i]);
                    areaPath.Close();

                    using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = parsedAreaColor.WithAlpha(45), IsAntialias = true };
                    canvas.DrawPath(areaPath, fillPaint);
                    using var strokePaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke, Color = parsedAreaColor, StrokeWidth = 2.5f, IsAntialias = true,
                        PathEffect = SKPathEffect.CreateDash(new[] { 12f, 6f }, 0)
                    };
                    canvas.DrawPath(areaPath, strokePaint);
                }

                foreach (var track in validTracks)
                {
                    var trackPts = track.Points.Select(p => new SKPoint(ToX(p.Longitude), ToY(p.Latitude))).ToArray();
                    using var trackPath = new SKPath();
                    trackPath.MoveTo(trackPts[0]);
                    for (var i = 1; i < trackPts.Length; i++) trackPath.LineTo(trackPts[i]);

                    using var shadowPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke, Color = new SKColor(0, 0, 0, 65), StrokeWidth = 6f,
                        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true
                    };
                    canvas.DrawPath(trackPath, shadowPaint);

                    using var trackPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke, Color = ParseColor(track.Color), StrokeWidth = 3.5f,
                        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true
                    };
                    canvas.DrawPath(trackPath, trackPaint);
                }

                if (elwPosition.HasValue)
                    DrawMarker(canvas, new SKPoint(ToX(elwPosition.Value.Longitude), ToY(elwPosition.Value.Latitude)), new SKColor(220, 20, 60), "ELW");

                DrawAttribution(canvas, width, height, "© OpenStreetMap © CARTO");
                return EncodeAsPng(outputBitmap);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Rendern der kombinierten Track-Karte");
            return null;
        }
    }

    public async Task<byte[]?> RenderSearchAreaMapAsync(
        List<SearchArea> searchAreas,
        (double Latitude, double Longitude)? elwPosition,
        MapTileType tileType = MapTileType.Streets,
        int width = 1500,
        int height = 1060)
    {
        var validAreas = searchAreas.Where(a => a.Coordinates?.Count >= 3).ToList();
        if (validAreas.Count == 0 && !elwPosition.HasValue)
            return null;

        var tileConfig = GetSearchAreaTileConfig(tileType);
        var tilePixelSize = tileConfig.PixelSize;

        try
        {
            var allLats = validAreas.SelectMany(a => a.Coordinates.Select(c => c.Latitude)).ToList();
            var allLons = validAreas.SelectMany(a => a.Coordinates.Select(c => c.Longitude)).ToList();

            if (elwPosition.HasValue) { allLats.Add(elwPosition.Value.Latitude); allLons.Add(elwPosition.Value.Longitude); }
            if (allLats.Count == 0) return null;

            var minLat = allLats.Min(); var maxLat = allLats.Max();
            var minLon = allLons.Min(); var maxLon = allLons.Max();

            var latPad = Math.Max((maxLat - minLat) * 0.12, 0.002);
            var lonPad = Math.Max((maxLon - minLon) * 0.12, 0.003);
            minLat -= latPad; maxLat += latPad;
            minLon -= lonPad; maxLon += lonPad;

            var zoom = CalculateZoom(minLat, maxLat, minLon, maxLon, width, height);

            var absCropLeft = LonToTileXFloat(minLon, zoom) * tilePixelSize;
            var absCropTop = LatToTileYFloat(maxLat, zoom) * tilePixelSize;
            var absCropRight = LonToTileXFloat(maxLon, zoom) * tilePixelSize;
            var absCropBottom = LatToTileYFloat(minLat, zoom) * tilePixelSize;

            AdjustCropToAspect(ref absCropLeft, ref absCropTop, ref absCropRight, ref absCropBottom, width, height);

            var minTileX = (int)Math.Floor(absCropLeft / tilePixelSize);
            var maxTileX = (int)Math.Floor(absCropRight / tilePixelSize);
            var minTileY = (int)Math.Floor(absCropTop / tilePixelSize);
            var maxTileY = (int)Math.Floor(absCropBottom / tilePixelSize);

            var tiles = await DownloadTilesWithConfigAsync(minTileX, maxTileX, minTileY, maxTileY, zoom, tileConfig.UrlTemplates);
            var (fullBitmap, _, _) = AssembleTileMosaic(tiles, minTileX, minTileY, maxTileX, maxTileY, tilePixelSize);

            using (fullBitmap)
            {
                var cropLeft = (float)(absCropLeft - minTileX * tilePixelSize);
                var cropTop = (float)(absCropTop - minTileY * tilePixelSize);
                var cropRight = (float)(absCropRight - minTileX * tilePixelSize);
                var cropBottom = (float)(absCropBottom - minTileY * tilePixelSize);

                using var outputBitmap = new SKBitmap(width, height);
                using var canvas = new SKCanvas(outputBitmap);
                canvas.DrawBitmap(fullBitmap, new SKRect(cropLeft, cropTop, cropRight, cropBottom), new SKRect(0, 0, width, height));

                var scaleX = width / (cropRight - cropLeft);
                var scaleY = height / (cropBottom - cropTop);
                float ToX(double lon) => ((float)((LonToTileXFloat(lon, zoom) - minTileX) * tilePixelSize) - cropLeft) * scaleX;
                float ToY(double lat) => ((float)((LatToTileYFloat(lat, zoom) - minTileY) * tilePixelSize) - cropTop) * scaleY;

                // Erst alle Füllungen, dann alle Ränder, dann Labels
                foreach (var area in validAreas)
                {
                    var areaPoints = area.Coordinates.Select(c => new SKPoint(ToX(c.Longitude), ToY(c.Latitude))).ToArray();
                    var color = ParseColor(string.IsNullOrWhiteSpace(area.Color) ? "#2196F3" : area.Color);
                    using var areaPath = new SKPath();
                    areaPath.MoveTo(areaPoints[0]);
                    for (var i = 1; i < areaPoints.Length; i++) areaPath.LineTo(areaPoints[i]);
                    areaPath.Close();
                    using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = color.WithAlpha(55), IsAntialias = true };
                    canvas.DrawPath(areaPath, fillPaint);
                }

                foreach (var area in validAreas)
                {
                    var areaPoints = area.Coordinates.Select(c => new SKPoint(ToX(c.Longitude), ToY(c.Latitude))).ToArray();
                    var color = ParseColor(string.IsNullOrWhiteSpace(area.Color) ? "#2196F3" : area.Color);
                    using var areaPath = new SKPath();
                    areaPath.MoveTo(areaPoints[0]);
                    for (var i = 1; i < areaPoints.Length; i++) areaPath.LineTo(areaPoints[i]);
                    areaPath.Close();
                    using var strokePaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = color, StrokeWidth = 3f, IsAntialias = true };
                    canvas.DrawPath(areaPath, strokePaint);
                }

                foreach (var area in validAreas)
                {
                    var color = ParseColor(string.IsNullOrWhiteSpace(area.Color) ? "#2196F3" : area.Color);
                    DrawAreaLabel(canvas, new SKPoint(ToX(area.Coordinates.Average(c => c.Longitude)), ToY(area.Coordinates.Average(c => c.Latitude))), area.Name, color);
                }

                if (elwPosition.HasValue)
                    DrawMarker(canvas, new SKPoint(ToX(elwPosition.Value.Longitude), ToY(elwPosition.Value.Latitude)), new SKColor(220, 20, 60), "ELW");

                DrawNorthArrow(canvas, width, height);
                DrawAttribution(canvas, width, height, tileConfig.Attribution);

                using var image = SKImage.FromBitmap(outputBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 92);
                return data.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Rendern der Suchgebiets-Planungskarte");
            return null;
        }
    }
}
