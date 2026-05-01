using Einsatzueberwachung.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Einsatzueberwachung.Server.Services;

public sealed partial class OsmStaticMapRenderer : IStaticMapRenderer, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsmStaticMapRenderer> _logger;

    // Carto @2x Tiles sind 512×512px
    private const int TileSize = 512;

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
