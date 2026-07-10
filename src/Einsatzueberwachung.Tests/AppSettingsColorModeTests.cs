using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Tests;

public class AppSettingsColorModeTests
{
    [Fact]
    public void CollarColorModes_DefaultAndInvalidValuesUseAreaColor()
    {
        var settings = new AppSettings();

        Assert.Equal("area", settings.CollarTrackColorModeOrDefault);
        Assert.Equal("area", settings.CollarMarkerColorModeOrDefault);

        settings.CollarTrackColorMode = "invalid";
        settings.CollarMarkerColorMode = "contrast";

        Assert.Equal("area", settings.CollarTrackColorModeOrDefault);
        Assert.Equal("contrast", settings.CollarMarkerColorModeOrDefault);
    }
}
