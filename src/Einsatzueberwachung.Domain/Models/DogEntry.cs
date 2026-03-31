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
        public string HundefuehrerId { get; set; }
        public string Notizen { get; set; }
        public bool IsActive { get; set; }

        public DogEntry()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Rasse = string.Empty;
            Specializations = DogSpecialization.None;
            HundefuehrerId = string.Empty;
            Notizen = string.Empty;
            IsActive = true;
            Alter = 0;
        }

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
