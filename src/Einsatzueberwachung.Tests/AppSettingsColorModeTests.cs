using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Tests;

public class AppSettingsColorModeTests
{
    [Fact]
    public void CollarColorModes_DefaultAndInvalidValuesUseConfiguredDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("black", settings.CollarTrackColorModeOrDefault);
        Assert.Equal("area-black-outline", settings.CollarMarkerColorModeOrDefault);

        settings.CollarTrackColorMode = "invalid";
        settings.CollarMarkerColorMode = "contrast";

        Assert.Equal("black", settings.CollarTrackColorModeOrDefault);
        Assert.Equal("contrast", settings.CollarMarkerColorModeOrDefault);
    }
}
