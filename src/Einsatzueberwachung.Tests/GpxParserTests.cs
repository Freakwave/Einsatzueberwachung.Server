using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Tests;

public class GpxParserTests
{
    // --- trkpt (Standard-Trackpunkte) ---

    [Fact]
    public void Parse_Trkpt_Gpx11_ReturnsTwoPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.1" lon="11.5"><time>2024-06-01T10:00:00Z</time></trkpt>
                <trkpt lat="48.2" lon="11.6"><time>2024-06-01T10:01:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
        Assert.Equal(48.1, pts[0].Latitude, precision: 5);
        Assert.Equal(11.5, pts[0].Longitude, precision: 5);
    }

    [Fact]
    public void Parse_Trkpt_NoNamespace_ReturnsTwoPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1">
              <trk><trkseg>
                <trkpt lat="51.0" lon="7.0"><time>2024-06-01T08:00:00Z</time></trkpt>
                <trkpt lat="51.1" lon="7.1"><time>2024-06-01T08:05:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
    }

    // --- wpt (Wegpunkte als Fallback) ---

    [Fact]
    public void Parse_WptFallback_ReturnsPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <wpt lat="48.5" lon="11.9"><time>2024-06-01T09:00:00Z</time></wpt>
              <wpt lat="48.6" lon="12.0"><time>2024-06-01T09:10:00Z</time></wpt>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
    }

    // --- rtept (Routenpunkte als zweiter Fallback) ---

    [Fact]
    public void Parse_RteptFallback_Gpx11_ReturnsPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <rte>
                <rtept lat="52.5" lon="13.4"><time>2024-06-01T07:00:00Z</time></rtept>
                <rtept lat="52.6" lon="13.5"><time>2024-06-01T07:10:00Z</time></rtept>
              </rte>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
        Assert.Equal(52.5, pts[0].Latitude, precision: 5);
    }

    [Fact]
    public void Parse_RteptFallback_NoNamespace_ReturnsPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1">
              <rte>
                <rtept lat="48.3" lon="11.7"><time>2024-06-01T11:00:00Z</time></rtept>
                <rtept lat="48.4" lon="11.8"><time>2024-06-01T11:05:00Z</time></rtept>
              </rte>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
    }

    [Fact]
    public void Parse_RteptFallback_Gpx10_ReturnsPoints()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.0" xmlns="http://www.topografix.com/GPX/1/0">
              <rte>
                <rtept lat="47.0" lon="8.0"></rtept>
                <rtept lat="47.1" lon="8.1"></rtept>
              </rte>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        Assert.Equal(2, pts.Count);
    }

    // --- Priorität: trkpt vor rtept ---

    [Fact]
    public void Parse_PrefersTrkptOverRtept_WhenBothPresent()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="10.0" lon="10.0"><time>2024-06-01T10:00:00Z</time></trkpt>
              </trkseg></trk>
              <rte>
                <rtept lat="20.0" lon="20.0"><time>2024-06-01T11:00:00Z</time></rtept>
                <rtept lat="21.0" lon="21.0"><time>2024-06-01T11:10:00Z</time></rtept>
              </rte>
            </gpx>
            """;

        var pts = GpxParser.Parse(gpx);

        // Nur der trkpt, nicht die rtept
        Assert.Single(pts);
        Assert.Equal(10.0, pts[0].Latitude, precision: 5);
    }

    // --- Fehlerfall ---

    [Fact]
    public void Parse_EmptyGpx_ThrowsFormatException()
    {
        const string gpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
            </gpx>
            """;

        Assert.Throws<FormatException>(() => GpxParser.Parse(gpx));
    }
}
