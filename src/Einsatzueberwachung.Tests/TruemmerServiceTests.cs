using System;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Services;
using Xunit;

namespace Einsatzueberwachung.Tests;

public class TruemmerServiceTests
{
    [Fact]
    public async Task AddKarte_storesKarte_andFiresEvent()
    {
        var svc = new EinsatzService();
        TruemmerKarte? added = null;
        svc.TruemmerKarteAdded += k => added = k;

        var karte = new TruemmerKarte { Title = "Drohne 1", ImageWidthPx = 1024, ImageHeightPx = 768 };
        await svc.AddTruemmerKarteAsync(karte);

        Assert.Single(svc.CurrentEinsatz.TruemmerKarten);
        Assert.Equal(karte.Id, added!.Id);
    }

    [Fact]
    public async Task RemoveKarte_alsoRemovesAreas()
    {
        var svc = new EinsatzService();
        var karte = new TruemmerKarte { Title = "K1", ImageWidthPx = 100, ImageHeightPx = 100 };
        await svc.AddTruemmerKarteAsync(karte);

        await svc.UpsertTruemmerAreaAsync(new TruemmerArea
        {
            TruemmerKarteId = karte.Id,
            Name = "A1",
            Points = new() { new(0, 0), new(50, 0), new(50, 50) }
        });
        Assert.Single(svc.CurrentEinsatz.TruemmerAreas);

        await svc.RemoveTruemmerKarteAsync(karte.Id);
        Assert.Empty(svc.CurrentEinsatz.TruemmerKarten);
        Assert.Empty(svc.CurrentEinsatz.TruemmerAreas);
    }

    [Fact]
    public async Task UpsertArea_updatesExisting_byId()
    {
        var svc = new EinsatzService();
        var area = new TruemmerArea { Name = "Erst", Points = new() { new(0, 0), new(1, 0), new(1, 1) } };
        await svc.UpsertTruemmerAreaAsync(area);

        area.Name = "Zweit";
        await svc.UpsertTruemmerAreaAsync(area);

        Assert.Single(svc.CurrentEinsatz.TruemmerAreas);
        Assert.Equal("Zweit", svc.CurrentEinsatz.TruemmerAreas[0].Name);
    }

    [Fact]
    public async Task RemoveArea_dropsEntry()
    {
        var svc = new EinsatzService();
        var area = new TruemmerArea { Name = "X", Points = new() { new(0, 0), new(1, 0), new(1, 1) } };
        await svc.UpsertTruemmerAreaAsync(area);

        Guid? removedId = null;
        svc.TruemmerAreaRemoved += id => removedId = id;

        await svc.RemoveTruemmerAreaAsync(area.Id);

        Assert.Empty(svc.CurrentEinsatz.TruemmerAreas);
        Assert.Equal(area.Id, removedId);
    }
}
