// Quelle: WPF-Projekt Models/PersonalEntry.cs
// Repräsentiert eine Person mit Vor-/Nachname, Qualifikationen und Status

using System;
using System.Collections.Generic;
using System.Linq;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Models
{
    public class PersonalEntry
    {
        public string Id { get; set; }
        public string Vorname { get; set; }
        public string Nachname { get; set; }
        public PersonalSkills Skills { get; set; }
        public string Notizen { get; set; }
        public bool IsActive { get; set; }

        public PersonalEntry()
        {
            Id = Guid.NewGuid().ToString();
            Vorname = string.Empty;
            Nachname = string.Empty;
            Skills = PersonalSkills.None;
            Notizen = string.Empty;
            IsActive = true;
        }

        public string FullName => $"{Vorname} {Nachname}".Trim();

        public string SkillsDisplay
        {
            get
            {
                if (Skills == PersonalSkills.None)
                    return "Keine Fähigkeiten";

                var skillList = new List<string>();
                foreach (PersonalSkills skill in Enum.GetValues(typeof(PersonalSkills)))
                {
                    if (skill != PersonalSkills.None && Skills.HasFlag(skill))
                    {
                        skillList.Add(skill.GetDisplayName());
                    }
                }
                return string.Join(", ", skillList);
            }
        }

        public string SkillsShortDisplay
        {
            get
            {
                if (Skills == PersonalSkills.None)
                    return "-";

                var skillList = new List<string>();
                foreach (PersonalSkills skill in Enum.GetValues(typeof(PersonalSkills)))
                {
                    if (skill != PersonalSkills.None && Skills.HasFlag(skill))
                    {
                        skillList.Add(skill.GetShortName());
                    }
                }
                return string.Join(", ", skillList);
            }
        }
    }
}
