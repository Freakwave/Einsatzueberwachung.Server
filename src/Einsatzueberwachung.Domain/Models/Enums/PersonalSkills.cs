// Quelle: WPF-Projekt Models/PersonalSkills.cs
// Beschreibt die Rollen/Qualifikationen von Personal (Hundeführer, Helfer, Führungsebenen, etc.)

using System;

namespace Einsatzueberwachung.Domain.Models.Enums
{
    [Flags]
    public enum PersonalSkills
    {
        None = 0,
        Hundefuehrer = 1 << 0,       // 1
        Helfer = 1 << 1,              // 2
        Fuehrungsassistent = 1 << 2,  // 4
        Gruppenfuehrer = 1 << 3,      // 8
        Zugfuehrer = 1 << 4,          // 16
        Verbandsfuehrer = 1 << 5,     // 32
        Drohnenpilot = 1 << 6,        // 64
        Einsatzleiter = 1 << 7        // 128
    }

    public static class PersonalSkillsExtensions
    {
        public static string GetDisplayName(this PersonalSkills skill)
        {
            return skill switch
            {
                PersonalSkills.Hundefuehrer => "Hundeführer",
                PersonalSkills.Helfer => "Helfer",
                PersonalSkills.Fuehrungsassistent => "Führungsassistent",
                PersonalSkills.Gruppenfuehrer => "Gruppenführer",
                PersonalSkills.Zugfuehrer => "Zugführer",
                PersonalSkills.Verbandsfuehrer => "Verbandsführer",
                PersonalSkills.Drohnenpilot => "Drohnenpilot",
                PersonalSkills.Einsatzleiter => "Einsatzleiter",
                _ => skill.ToString()
            };
        }

        public static string GetShortName(this PersonalSkills skill)
        {
            return skill switch
            {
                PersonalSkills.Hundefuehrer => "HF",
                PersonalSkills.Helfer => "H",
                PersonalSkills.Fuehrungsassistent => "FA",
                PersonalSkills.Gruppenfuehrer => "GF",
                PersonalSkills.Zugfuehrer => "ZF",
                PersonalSkills.Verbandsfuehrer => "VF",
                PersonalSkills.Drohnenpilot => "DP",
                PersonalSkills.Einsatzleiter => "EL",
                _ => ""
            };
        }

        public static bool IsLeadershipQualified(this PersonalSkills skills)
        {
            return skills.HasFlag(PersonalSkills.Gruppenfuehrer) ||
                   skills.HasFlag(PersonalSkills.Zugfuehrer) ||
                   skills.HasFlag(PersonalSkills.Verbandsfuehrer) ||
                   skills.HasFlag(PersonalSkills.Einsatzleiter);
        }

        public static string GetHighestLeadershipLevel(this PersonalSkills skills)
        {
            if (skills.HasFlag(PersonalSkills.Verbandsfuehrer)) return "Verbandsführer";
            if (skills.HasFlag(PersonalSkills.Zugfuehrer)) return "Zugführer";
            if (skills.HasFlag(PersonalSkills.Gruppenfuehrer)) return "Gruppenführer";
            if (skills.HasFlag(PersonalSkills.Einsatzleiter)) return "Einsatzleiter";
            if (skills.HasFlag(PersonalSkills.Fuehrungsassistent)) return "Führungsassistent";
            return "Keine Führungsqualifikation";
        }
    }
}
