using System;
using System.Collections.Generic;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Models
{
    /// <summary>
    /// Vorlage einer szenarioabhängigen Checkliste pro Vermisstem
    /// (z. B. „Trümmer: Gebäudeteil/Stockwerk", „Mantrailer: Geruchsstück").
    /// Editierbar in den Stammdaten.
    /// </summary>
    public class ChecklistTemplate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public EinsatzSzenarioType Szenario { get; set; } = EinsatzSzenarioType.Unbestimmt;
        public string Name { get; set; } = string.Empty;
        public List<ChecklistItemDefinition> Items { get; set; } = new();

        /// <summary>Markiert vom System geseedete Defaults (zum Wiederherstellen).</summary>
        public bool IsDefault { get; set; }
    }

    public class ChecklistItemDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; } = string.Empty;
        public ChecklistItemType Type { get; set; } = ChecklistItemType.Bool;

        /// <summary>Nur relevant für Type = Choice.</summary>
        public List<string> Choices { get; set; } = new();

        public bool Required { get; set; }
    }

    /// <summary>
    /// Konkrete Instanz pro Vermisstem: Verweis auf Template + Antworten je Item.
    /// Antworten sind Strings: bei Bool "true"/"false", bei Choice der gewählte Wert.
    /// </summary>
    public class ChecklistInstance
    {
        public Guid TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public EinsatzSzenarioType Szenario { get; set; } = EinsatzSzenarioType.Unbestimmt;

        /// <summary>Snapshot der Item-Definitionen zum Zeitpunkt der Erstellung — bleibt stabil, auch wenn das Template später geändert wird.</summary>
        public List<ChecklistItemDefinition> Items { get; set; } = new();

        /// <summary>ItemId → Antwort (String).</summary>
        public Dictionary<string, string?> Values { get; set; } = new();

        public static ChecklistInstance FromTemplate(ChecklistTemplate template)
        {
            return new ChecklistInstance
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                Szenario = template.Szenario,
                Items = template.Items.ConvertAll(it => new ChecklistItemDefinition
                {
                    Id = it.Id,
                    Label = it.Label,
                    Type = it.Type,
                    Choices = new List<string>(it.Choices),
                    Required = it.Required
                }),
                Values = new Dictionary<string, string?>()
            };
        }
    }
}
