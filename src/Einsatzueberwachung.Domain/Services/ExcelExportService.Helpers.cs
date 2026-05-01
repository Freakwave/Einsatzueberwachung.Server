using ClosedXML.Excel;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class ExcelExportService
    {
        private string GetSkillsString(PersonalSkills skills)
        {
            if (skills == PersonalSkills.None)
                return "";

            var skillList = new List<string>();
            foreach (PersonalSkills skill in Enum.GetValues(typeof(PersonalSkills)))
            {
                if (skill != PersonalSkills.None && skills.HasFlag(skill))
                    skillList.Add(skill.GetDisplayName());
            }
            return string.Join(", ", skillList);
        }

        private string GetSpecializationsString(DogSpecialization specs)
        {
            if (specs == DogSpecialization.None)
                return "";

            var specList = new List<string>();
            foreach (DogSpecialization spec in Enum.GetValues(typeof(DogSpecialization)))
            {
                if (spec != DogSpecialization.None && specs.HasFlag(spec))
                    specList.Add(spec.GetDisplayName());
            }
            return string.Join(", ", specList);
        }

        private PersonalSkills ParseSkills(string skillsString)
        {
            if (string.IsNullOrWhiteSpace(skillsString))
                return PersonalSkills.None;

            var result = PersonalSkills.None;
            var parts = skillsString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim().ToLowerInvariant();

                if (trimmed.Contains("hundeführer") || trimmed.Contains("hundefuehrer") || trimmed == "hf")
                    result |= PersonalSkills.Hundefuehrer;
                else if (trimmed.Contains("helfer") || trimmed == "h")
                    result |= PersonalSkills.Helfer;
                else if (trimmed.Contains("führungsassistent") || trimmed.Contains("fuehrungsassistent") || trimmed == "fa")
                    result |= PersonalSkills.Fuehrungsassistent;
                else if (trimmed.Contains("gruppenführer") || trimmed.Contains("gruppenfuehrer") || trimmed == "gf")
                    result |= PersonalSkills.Gruppenfuehrer;
                else if (trimmed.Contains("zugführer") || trimmed.Contains("zugfuehrer") || trimmed == "zf")
                    result |= PersonalSkills.Zugfuehrer;
                else if (trimmed.Contains("verbandsführer") || trimmed.Contains("verbandsfuehrer") || trimmed == "vf")
                    result |= PersonalSkills.Verbandsfuehrer;
                else if (trimmed.Contains("drohnenpilot") || trimmed.Contains("drohnen") || trimmed == "dp")
                    result |= PersonalSkills.Drohnenpilot;
                else if (trimmed.Contains("einsatzleiter") || trimmed.Contains("leiter") || trimmed == "el")
                    result |= PersonalSkills.Einsatzleiter;
            }

            return result;
        }

        private DogSpecialization ParseSpecializations(string specString)
        {
            if (string.IsNullOrWhiteSpace(specString))
                return DogSpecialization.None;

            var result = DogSpecialization.None;
            var parts = specString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim().ToLowerInvariant();

                if (trimmed.Contains("flächen") || trimmed.Contains("flaechen"))
                    result |= DogSpecialization.Flaechensuche;
                else if (trimmed.Contains("trümmer") || trimmed.Contains("truemmer"))
                    result |= DogSpecialization.Truemmersuche;
                else if (trimmed.Contains("wasser"))
                    result |= DogSpecialization.Wasserortung;
                else if (trimmed.Contains("mantrail"))
                    result |= DogSpecialization.Mantrailing;
                else if (trimmed.Contains("lawine"))
                    result |= DogSpecialization.Lawinensuche;
            }

            return result;
        }

        private PersonalEntry? FindPersonByName(List<PersonalEntry> personalList, string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return null;

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            var exact = personalList.FirstOrDefault(p =>
                p.FullName.Equals(fullName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            if (parts.Length >= 2)
            {
                var vorname = parts[0];
                var nachname = string.Join(" ", parts.Skip(1));

                var match = personalList.FirstOrDefault(p =>
                    p.Vorname.Equals(vorname, StringComparison.OrdinalIgnoreCase) &&
                    p.Nachname.Equals(nachname, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            return personalList.FirstOrDefault(p =>
                p.FullName.Contains(fullName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                fullName.Trim().Contains(p.FullName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var lower = value.Trim().ToLowerInvariant();
            return lower == "ja" || lower == "yes" || lower == "true" || lower == "1" || lower == "aktiv" || lower == "x";
        }

        private static int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return int.TryParse(value.Trim(), out var result) ? result : 0;
        }

        private static int? ParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim().TrimStart('#');
            return int.TryParse(normalized, out var result) ? result : null;
        }

        private static int? GetColumnIndexByHeader(IXLWorksheet ws, params string[] headerNames)
        {
            var usedCells = ws.Row(1).CellsUsed();

            foreach (var cell in usedCells)
            {
                var current = cell.GetString().Trim();
                foreach (var name in headerNames)
                {
                    if (current.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return cell.Address.ColumnNumber;
                }
            }

            return null;
        }
    }
}
