using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Services;

namespace Einsatzueberwachung.Tests;

public class GpxBuilderTests
{
    [Fact]
    public void SearchAreaFileName_StartsWithDayAndMonthAndFitsGarminLimit()
    {
        var area = new SearchArea { Name = "Sehr langes Suchgebiet mit Zusatz" };

        var fileName = GpxBuilder.SearchAreaFileName(area);

        Assert.StartsWith($"{DateTime.Now:ddMM}_", fileName);
        Assert.Equal(".gpx", Path.GetExtension(fileName));
        Assert.Equal(20, Path.GetFileNameWithoutExtension(fileName).Length);
    }

    [Fact]
    public void SearchAreaFileName_PreservesShortAreaNameAfterDate()
    {
        var area = new SearchArea { Name = "Suchgebiet1" };

        var fileName = GpxBuilder.SearchAreaFileName(area);

        Assert.Equal($"{DateTime.Now:ddMM}_Suchgebiet1.gpx", fileName);
    }
}
