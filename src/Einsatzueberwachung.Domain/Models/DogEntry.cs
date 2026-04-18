// Quelle: WPF-Projekt Models/DogEntry.cs
// Repräsentiert einen Hund mit Name, Rasse, Ausbildungen und zugeordnetem Hundeführer

using System;
using System.Collections.Generic;
using System.Linq;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Models
{
    public class DogEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Rasse { get; set; }
        public int Alter { get; set; }
        public DogSpecialization Specializations { get; set; }
        /// <summary>Liste aller Hundeführer-IDs (ein Hund kann mehrere Hundeführer haben)</summary>
        public List<string> HundefuehrerIds { get; set; } = new();
        public string Notizen { get; set; }
        public bool IsActive { get; set; }

        /// <summary>Legacy-Feld für JSON-Rückwärtskompatibilität. Wird beim Laden in HundefuehrerIds migriert.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("HundefuehrerId")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? HundefuehrerIdLegacy { get; set; }

        public DogEntry()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Rasse = string.Empty;
            Specializations = DogSpecialization.None;
            HundefuehrerIds = new List<string>();
            Notizen = string.Empty;
            IsActive = true;
            Alter = 0;
        }

        /// <summary>Erster (primärer) Hundeführer – für Abwärtskompatibilität mit EinsatzMonitor</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string PrimaryHundefuehrerId => HundefuehrerIds.Count > 0 ? HundefuehrerIds[0] : string.Empty;

        public string SpecializationsDisplay
        {
            get
            {
                if (Specializations == DogSpecialization.None)
                    return "Keine Spezialisierung";

                var specList = new List<string>();
                foreach (DogSpecialization spec in Enum.GetValues(typeof(DogSpecialization)))
                {
                    if (spec != DogSpecialization.None && Specializations.HasFlag(spec))
                    {
                        specList.Add(spec.GetDisplayName());
                    }
                }
                return string.Join(", ", specList);
            }
        }

        public string SpecializationsShortDisplay
        {
            get
            {
                if (Specializations == DogSpecialization.None)
                    return "-";

                var specList = new List<string>();
                foreach (DogSpecialization spec in Enum.GetValues(typeof(DogSpecialization)))
                {
                    if (spec != DogSpecialization.None && Specializations.HasFlag(spec))
                    {
                        specList.Add(spec.GetShortName());
                    }
                }
                return string.Join(", ", specList);
            }
        }

        public string PrimarySpecializationColor => Specializations.GetColorHex();
    }
}
