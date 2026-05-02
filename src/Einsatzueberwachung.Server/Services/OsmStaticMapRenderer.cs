using Einsatzueberwachung.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Einsatzueberwachung.Server.Services;

public sealed partial class OsmStaticMapRenderer : IStaticMapRenderer, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsmStaticMapRenderer> _logger;

    // Carto @2x Tiles sind 512×512px
    private const int TileSize = 512;

    // Gesamt-Timeout pro Map-Render. Schützt vor 502 Bad Gateway durch Nginx (60s default)
    // und davor, dass langsame externe Tile-Server (ESRI/OpenTopoMap) den Worker blockieren.
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(25);

    // Per-Tile-Timeout: deutlich kürzer als HttpClient.Timeout, damit ein hängender Server
    // nicht das Gesamt-Timeout aufbraucht.
    private static readonly TimeSpan TileTimeout = TimeSpan.FromSeconds(6);

    // Globale Drosselung: nur ein Map-Render gleichzeitig. Mehrfach-Klick auf "Drucken"
    // stapelt Renderings nicht mehr und verhindert ThreadPool-/Speicher-Druck.
    private static readonly SemaphoreSlim _globalRenderLock = new(1, 1);

    public OsmStaticMapRenderer(IHttpClientFactory httpClientFactory, ILogger<OsmStaticMapRenderer> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OsmTiles");
        _logger = logger;
    }

    public void Dispose()
    {
        // HttpClient wird vom Factory verwaltet
    }
}
