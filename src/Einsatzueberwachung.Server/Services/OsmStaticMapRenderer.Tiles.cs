using Einsatzueberwachung.Domain.Models.Enums;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Einsatzueberwachung.Server.Services;

public sealed partial class OsmStaticMapRenderer
{
    private record SearchAreaTileConfig(string[] UrlTemplates, int PixelSize, string Attribution);

    private static SearchAreaTileConfig GetSearchAreaTileConfig(MapTileType tileType) => tileType switch
    {
        MapTileType.Satellite => new(
            ["https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}"],
            256,
            "© Esri, Maxar, Earthstar Geographics"),

        MapTileType.Topographic => new(
            [
                "https://a.tile.opentopomap.org/{z}/{x}/{y}.png",
                "https://b.tile.opentopomap.org/{z}/{x}/{y}.png",
                "https://c.tile.opentopomap.org/{z}/{x}/{y}.png"
            ],
            256,
            "© OpenStreetMap contributors | © OpenTopoMap (CC-BY-SA)"),

        _ => new(
            [
                "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png",
                "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png",
                "https://b.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png"
            ],
            512,
            "© OpenStreetMap © CARTO")
    };

    private static readonly string[] TileServers =
    [
        "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png",
        "https://a.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png",
        "https://b.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}@2x.png",
    ];

    private async Task<Dictionary<(int x, int y), byte[]?>> DownloadTilesAsync(
        int minX, int maxX, int minY, int maxY, int zoom)
    {
        var semaphore = new SemaphoreSlim(2);
        var tasks = new List<(int x, int y, Task<byte[]?> task)>();

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                var cx = x; var cy = y;
                tasks.Add((cx, cy, ThrottledDownloadAsync(semaphore, cx, cy, zoom)));
            }

        await Task.WhenAll(tasks.Select(t => t.task));
        semaphore.Dispose();

        return tasks.ToDictionary(t => (t.x, t.y), t => t.task.Result);
    }

    private async Task<byte[]?> ThrottledDownloadAsync(SemaphoreSlim semaphore, int x, int y, int zoom)
    {
        await semaphore.WaitAsync();
        try { return await DownloadTileAsync(x, y, zoom); }
        finally { semaphore.Release(); }
    }

    private async Task<byte[]?> DownloadTileAsync(int x, int y, int zoom)
    {
        foreach (var template in TileServers)
        {
            try
            {
                var url = template.Replace("{z}", zoom.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString());
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tile {Zoom}/{X}/{Y} konnte nicht geladen werden", zoom, x, y);
            }
        }
        return null;
    }

    private async Task<Dictionary<(int x, int y), byte[]?>> DownloadTilesWithConfigAsync(
        int minX, int maxX, int minY, int maxY, int zoom, string[] urlTemplates)
    {
        var semaphore = new SemaphoreSlim(2);
        var tasks = new List<(int x, int y, Task<byte[]?> task)>();

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                var cx = x; var cy = y;
                tasks.Add((cx, cy, ThrottledDownloadWithConfigAsync(semaphore, cx, cy, zoom, urlTemplates)));
            }

        await Task.WhenAll(tasks.Select(t => t.task));
        semaphore.Dispose();

        return tasks.ToDictionary(t => (t.x, t.y), t => t.task.Result);
    }

    private async Task<byte[]?> ThrottledDownloadWithConfigAsync(SemaphoreSlim semaphore, int x, int y, int zoom, string[] urlTemplates)
    {
        await semaphore.WaitAsync();
        try { return await DownloadTileWithConfigAsync(x, y, zoom, urlTemplates); }
        finally { semaphore.Release(); }
    }

    private async Task<byte[]?> DownloadTileWithConfigAsync(int x, int y, int zoom, string[] urlTemplates)
    {
        foreach (var template in urlTemplates)
        {
            try
            {
                var url = template.Replace("{z}", zoom.ToString()).Replace("{x}", x.ToString()).Replace("{y}", y.ToString());
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tile {Zoom}/{X}/{Y} konnte nicht geladen werden", zoom, x, y);
            }
        }
        return null;
    }

    private static (SKBitmap bitmap, int w, int h) AssembleTileMosaic(
        Dictionary<(int x, int y), byte[]?> tiles,
        int minTileX, int minTileY, int maxTileX, int maxTileY, int pixelSize)
    {
        var tileCountX = maxTileX - minTileX + 1;
        var tileCountY = maxTileY - minTileY + 1;
        var fullW = tileCountX * pixelSize;
        var fullH = tileCountY * pixelSize;

        var bitmap = new SKBitmap(fullW, fullH);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(228, 228, 228));

        foreach (var ((tx, ty), tileData) in tiles)
        {
            if (tileData == null) continue;
            using var tileBitmap = SKBitmap.Decode(tileData);
            if (tileBitmap == null) continue;
            canvas.DrawBitmap(tileBitmap, (tx - minTileX) * pixelSize, (ty - minTileY) * pixelSize);
        }

        return (bitmap, fullW, fullH);
    }

    private static void AdjustCropToAspect(
        ref double left, ref double top, ref double right, ref double bottom,
        int targetWidth, int targetHeight)
    {
        var cropW = right - left;
        var cropH = bottom - top;
        var targetAspect = (double)targetWidth / targetHeight;
        var cropAspect = cropW / cropH;

        if (cropAspect < targetAspect)
        {
            var delta = (cropH * targetAspect - cropW) / 2.0;
            left -= delta; right += delta;
        }
        else
        {
            var delta = (cropW / targetAspect - cropH) / 2.0;
            top -= delta; bottom += delta;
        }
    }

    private static byte[] EncodeAsPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
