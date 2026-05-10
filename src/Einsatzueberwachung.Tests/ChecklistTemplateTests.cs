using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Domain.Services;
using Xunit;

namespace Einsatzueberwachung.Tests;

/// <summary>
/// In-Memory Stub: implementiert die Template-Methoden, die in Phase 3 neu sind.
/// Für die Personal/Hund/Drohnen-Methoden delegiert er an die Felder im FakeMasterDataService aus EinsatzMergeServiceRevertTests.
/// </summary>
internal sealed class FakeMasterDataServiceWithTemplates : IMasterDataService
{
    public List<PersonalEntry> Personal { get; } = new();
    public List<DogEntry> Dogs { get; } = new();
    public List<DroneEntry> Drones { get; } = new();
    public List<ChecklistTemplate> Templates { get; } = new();
    public SessionData Session { get; } = new();

    public Task<List<PersonalEntry>> GetPersonalListAsync() => Task.FromResult(Personal.ToList());
    public Task<PersonalEntry?> GetPersonalByIdAsync(string id) => Task.FromResult(Personal.FirstOrDefault(p => p.Id == id));
    public Task AddPersonalAsync(PersonalEntry personal) { Personal.Add(personal); return Task.CompletedTask; }
    public Task UpdatePersonalAsync(PersonalEntry personal) { return Task.CompletedTask; }
    public Task DeletePersonalAsync(string id) { Personal.RemoveAll(p => p.Id == id); return Task.CompletedTask; }

    public Task<List<DogEntry>> GetDogListAsync() => Task.FromResult(Dogs.ToList());
    public Task<DogEntry?> GetDogByIdAsync(string id) => Task.FromResult(Dogs.FirstOrDefault(d => d.Id == id));
    public Task AddDogAsync(DogEntry dog) { Dogs.Add(dog); return Task.CompletedTask; }
    public Task UpdateDogAsync(DogEntry dog) { return Task.CompletedTask; }
    public Task DeleteDogAsync(string id) { Dogs.RemoveAll(d => d.Id == id); return Task.CompletedTask; }

    public Task<List<DroneEntry>> GetDroneListAsync() => Task.FromResult(Drones.ToList());
    public Task<DroneEntry?> GetDroneByIdAsync(string id) => Task.FromResult(Drones.FirstOrDefault(d => d.Id == id));
    public Task AddDroneAsync(DroneEntry drone) { Drones.Add(drone); return Task.CompletedTask; }
    public Task UpdateDroneAsync(DroneEntry drone) { return Task.CompletedTask; }
    public Task DeleteDroneAsync(string id) { Drones.RemoveAll(d => d.Id == id); return Task.CompletedTask; }

    public Task<SessionData> LoadSessionDataAsync() => Task.FromResult(Session);
    public Task SaveSessionDataAsync(SessionData sessionData) => Task.CompletedTask;

    public Task<List<ChecklistTemplate>> GetChecklistTemplatesAsync() => Task.FromResult(Templates.ToList());
    public Task<ChecklistTemplate?> GetChecklistTemplateAsync(Guid id) => Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));
    public Task<ChecklistTemplate?> GetDefaultChecklistTemplateAsync(EinsatzSzenarioType szenario)
        => Task.FromResult(Templates.FirstOrDefault(t => t.Szenario == szenario && t.IsDefault)
                           ?? Templates.FirstOrDefault(t => t.Szenario == szenario));
    public Task AddChecklistTemplateAsync(ChecklistTemplate template) { Templates.Add(template); return Task.CompletedTask; }
    public Task UpdateChecklistTemplateAsync(ChecklistTemplate template)
    {
        var i = Templates.FindIndex(t => t.Id == template.Id);
        if (i >= 0) Templates[i] = template;
        return Task.CompletedTask;
    }
    public Task DeleteChecklistTemplateAsync(Guid id) { Templates.RemoveAll(t => t.Id == id); return Task.CompletedTask; }
}

public class ChecklistTemplateTests
{
    [Fact]
    public void FromTemplate_clonesItems_andStartsWithEmptyValues()
    {
        var template = new ChecklistTemplate
        {
            Szenario = EinsatzSzenarioType.Truemmer,
            Name = "Trümmer-Standard",
            Items = new List<ChecklistItemDefinition>
            {
                new() { Label = "Gebäudeteil", Type = ChecklistItemType.FreeText, Required = true },
                new() { Label = "Statik geprüft", Type = ChecklistItemType.Bool }
            }
        };

        var instance = ChecklistInstance.FromTemplate(template);

        Assert.Equal(template.Id, instance.TemplateId);
        Assert.Equal("Trümmer-Standard", instance.TemplateName);
        Assert.Equal(EinsatzSzenarioType.Truemmer, instance.Szenario);
        Assert.Equal(2, instance.Items.Count);
        Assert.Empty(instance.Values);

        // Item-Liste ist deep-cloned: Mutation am Template darf Instanz nicht beeinflussen
        template.Items[0].Label = "Geändert";
        Assert.Equal("Gebäudeteil", instance.Items[0].Label);
    }

    [Fact]
    public async Task UpsertVermissten_attachesDefaultTemplate_forCurrentSzenario()
    {
        var fake = new FakeMasterDataServiceWithTemplates();
        var template = new ChecklistTemplate
        {
            Szenario = EinsatzSzenarioType.Truemmer,
            Name = "Trümmer-Default",
            IsDefault = true,
            Items = new() { new() { Label = "Gebäudeteil", Type = ChecklistItemType.FreeText } }
        };
        fake.Templates.Add(template);

        var svc = new EinsatzService(masterDataService: fake);
        await svc.UpdateSzenarioAsync(EinsatzSzenarioType.Truemmer);

        var person = new VermisstenInfo { Vorname = "X" };
        await svc.UpsertVermisstenAsync(person);

        var stored = svc.CurrentEinsatz.Vermisste.Single();
        Assert.NotNull(stored.Checkliste);
        Assert.Equal(template.Id, stored.Checkliste!.TemplateId);
        Assert.Single(stored.Checkliste.Items);
    }

    [Fact]
    public async Task UpsertVermissten_doesNotOverwriteExistingChecklist()
    {
        var fake = new FakeMasterDataServiceWithTemplates();
        fake.Templates.Add(new ChecklistTemplate
        {
            Szenario = EinsatzSzenarioType.Flaeche,
            Name = "Default", IsDefault = true,
            Items = new() { new() { Label = "Default-Item", Type = ChecklistItemType.Bool } }
        });

        var svc = new EinsatzService(masterDataService: fake);
        await svc.UpdateSzenarioAsync(EinsatzSzenarioType.Flaeche);

        var customChecklist = new ChecklistInstance
        {
            TemplateName = "Custom",
            Szenario = EinsatzSzenarioType.Flaeche,
            Items = new() { new() { Label = "Custom-Item", Type = ChecklistItemType.FreeText } }
        };
        var person = new VermisstenInfo { Vorname = "Y", Checkliste = customChecklist };
        await svc.UpsertVermisstenAsync(person);

        Assert.Equal("Custom", svc.CurrentEinsatz.Vermisste.Single().Checkliste!.TemplateName);
    }

    [Fact]
    public async Task UpsertVermissten_withoutSzenario_doesNotAttach()
    {
        var fake = new FakeMasterDataServiceWithTemplates();
        fake.Templates.Add(new ChecklistTemplate
        {
            Szenario = EinsatzSzenarioType.Mantrailer,
            Name = "MT", IsDefault = true,
            Items = new() { new() { Label = "Item", Type = ChecklistItemType.Bool } }
        });

        var svc = new EinsatzService(masterDataService: fake);
        // kein Szenario gesetzt
        await svc.UpsertVermisstenAsync(new VermisstenInfo { Vorname = "Z" });

        Assert.Null(svc.CurrentEinsatz.Vermisste.Single().Checkliste);
    }
}
