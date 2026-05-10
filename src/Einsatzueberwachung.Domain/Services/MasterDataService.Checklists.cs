using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class MasterDataService
    {
        public async Task<List<ChecklistTemplate>> GetChecklistTemplatesAsync()
        {
            var data = await LoadSessionDataAsync();
            return data.ChecklistTemplates;
        }

        public async Task<ChecklistTemplate?> GetChecklistTemplateAsync(Guid id)
        {
            var list = await GetChecklistTemplatesAsync();
            return list.FirstOrDefault(t => t.Id == id);
        }

        public async Task<ChecklistTemplate?> GetDefaultChecklistTemplateAsync(EinsatzSzenarioType szenario)
        {
            var list = await GetChecklistTemplatesAsync();
            // Bevorzugt das als IsDefault markierte Template, sonst irgendein Template für das Szenario.
            return list.FirstOrDefault(t => t.Szenario == szenario && t.IsDefault)
                ?? list.FirstOrDefault(t => t.Szenario == szenario);
        }

        public async Task AddChecklistTemplateAsync(ChecklistTemplate template)
        {
            var data = await LoadSessionDataAsync();
            data.ChecklistTemplates.Add(template);
            await SaveSessionDataAsync(data);
        }

        public async Task UpdateChecklistTemplateAsync(ChecklistTemplate template)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.ChecklistTemplates.FirstOrDefault(t => t.Id == template.Id);
            if (existing is null) return;
            var index = data.ChecklistTemplates.IndexOf(existing);
            data.ChecklistTemplates[index] = template;
            await SaveSessionDataAsync(data);
        }

        public async Task DeleteChecklistTemplateAsync(Guid id)
        {
            var data = await LoadSessionDataAsync();
            var existing = data.ChecklistTemplates.FirstOrDefault(t => t.Id == id);
            if (existing is null) return;
            data.ChecklistTemplates.Remove(existing);
            await SaveSessionDataAsync(data);
        }

        /// <summary>
        /// Wenn für ein Szenario noch keinerlei Template existiert, wird das fest verdrahtete Default angelegt.
        /// User-eigene Templates oder geänderte Defaults werden nicht überschrieben.
        /// Liefert true, wenn etwas hinzugefügt wurde (Caller muss persistieren).
        /// </summary>
        private static bool SeedMissingDefaultChecklistTemplates(SessionData data)
        {
            var added = false;
            foreach (var sz in new[] { EinsatzSzenarioType.Mantrailer, EinsatzSzenarioType.Flaeche, EinsatzSzenarioType.Truemmer })
            {
                if (data.ChecklistTemplates.Any(t => t.Szenario == sz))
                    continue;

                data.ChecklistTemplates.Add(BuildDefaultTemplate(sz));
                added = true;
            }
            return added;
        }

        private static ChecklistTemplate BuildDefaultTemplate(EinsatzSzenarioType szenario)
        {
            return szenario switch
            {
                EinsatzSzenarioType.Mantrailer => new ChecklistTemplate
                {
                    Szenario = szenario,
                    Name = "Mantrailer-Standard",
                    IsDefault = true,
                    Items = new()
                    {
                        new() { Label = "Geruchsstück gesichert", Type = ChecklistItemType.Bool },
                        new() { Label = "Letzter sicherer Aufenthalt (LKP) bestätigt", Type = ChecklistItemType.Bool, Required = true },
                        new() { Label = "Spurenstart-Punkt definiert", Type = ChecklistItemType.FreeText },
                        new() { Label = "Ablenker / Störfaktoren bekannt", Type = ChecklistItemType.FreeText },
                        new() { Label = "Wind-/Wetterlage notiert", Type = ChecklistItemType.FreeText },
                    }
                },
                EinsatzSzenarioType.Flaeche => new ChecklistTemplate
                {
                    Szenario = szenario,
                    Name = "Fläche-Standard",
                    IsDefault = true,
                    Items = new()
                    {
                        new() { Label = "Suchgebiet abgegrenzt", Type = ChecklistItemType.Bool, Required = true },
                        new() { Label = "Geländeart", Type = ChecklistItemType.Choice,
                                Choices = new() { "Wald", "Feld/Wiese", "Bebauung", "Gewässer", "Misch" } },
                        new() { Label = "Bekannte Gefahrenstellen", Type = ChecklistItemType.FreeText },
                        new() { Label = "Letzte sichere Position erfasst", Type = ChecklistItemType.Bool },
                        new() { Label = "Zeitfenster Vermisstmeldung → jetzt", Type = ChecklistItemType.FreeText },
                    }
                },
                EinsatzSzenarioType.Truemmer => new ChecklistTemplate
                {
                    Szenario = szenario,
                    Name = "Trümmer-Standard",
                    IsDefault = true,
                    Items = new()
                    {
                        new() { Label = "Gebäudeteil / Sektor", Type = ChecklistItemType.FreeText, Required = true },
                        new() { Label = "Stockwerk / Ebene", Type = ChecklistItemType.FreeText },
                        new() { Label = "Letzter Aufenthaltsraum bekannt", Type = ChecklistItemType.FreeText },
                        new() { Label = "Bauart", Type = ChecklistItemType.Choice,
                                Choices = new() { "Wohnhaus", "Industrie", "Hochhaus", "Tiefgarage", "Sonstiges" } },
                        new() { Label = "Statik freigegeben (THW/Statiker)", Type = ChecklistItemType.Bool, Required = true },
                        new() { Label = "Gefahrstoffe (Gas/Strom/Wasser) abgeschaltet", Type = ChecklistItemType.Bool, Required = true },
                        new() { Label = "Lagekarte / Drohnenfoto vorhanden", Type = ChecklistItemType.Bool },
                    }
                },
                _ => new ChecklistTemplate { Szenario = szenario, Name = "Leer", IsDefault = true }
            };
        }
    }
}
