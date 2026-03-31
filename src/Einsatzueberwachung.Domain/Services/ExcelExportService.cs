// Excel Import/Export Service für Stammdaten
// Verwendet ClosedXML für Excel-Operationen

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly IMasterDataService _masterDataService;

        public ExcelExportService(IMasterDataService masterDataService)
        {
            _masterDataService = masterDataService;
        }

        public async Task<byte[]> ExportStammdatenAsync()
        {
            using var workbook = new XLWorkbook();

            // Personal exportieren
            var personalList = await _masterDataService.GetPersonalListAsync();
            await ExportPersonalSheet(workbook, personalList);

            // Hunde exportieren
            var dogList = await _masterDataService.GetDogListAsync();
            await ExportHundeSheet(workbook, dogList, personalList);

            // Drohnen exportieren
            var droneList = await _masterDataService.GetDroneListAsync();
            await ExportDrohnenSheet(workbook, droneList, personalList);

            // Als Byte-Array zurückgeben
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private Task ExportPersonalSheet(XLWorkbook workbook, List<PersonalEntry> personalList)
        {
            var ws = workbook.Worksheets.Add("Personal");

            // Header
            ws.Cell(1, 1).Value = "Vorname";
            ws.Cell(1, 2).Value = "Nachname";
            ws.Cell(1, 3).Value = "Qualifikationen";
            ws.Cell(1, 4).Value = "Notizen";
            ws.Cell(1, 5).Value = "Aktiv";

            // Header-Formatierung
            var headerRange = ws.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Daten
            int row = 2;
            foreach (var person in personalList.OrderBy(p => p.Nachname).ThenBy(p => p.Vorname))
            {
                ws.Cell(row, 1).Value = person.Vorname;
                ws.Cell(row, 2).Value = person.Nachname;
                ws.Cell(row, 3).Value = GetSkillsString(person.Skills);
                ws.Cell(row, 4).Value = person.Notizen;
                ws.Cell(row, 5).Value = person.IsActive ? "Ja" : "Nein";
                row++;
            }

            // Spaltenbreite automatisch anpassen
            ws.Columns().AdjustToContents();

            return Task.CompletedTask;
        }

        private Task ExportHundeSheet(XLWorkbook workbook, List<DogEntry> dogList, List<PersonalEntry> personalList)
        {
            var ws = workbook.Worksheets.Add("Hunde");

            // Header
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Rasse";
            ws.Cell(1, 3).Value = "Alter";
            ws.Cell(1, 4).Value = "Spezialisierungen";
            ws.Cell(1, 5).Value = "Hundeführer";
            ws.Cell(1, 6).Value = "Notizen";
            ws.Cell(1, 7).Value = "Aktiv";

            // Header-Formatierung
            var headerRange = ws.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Daten
            int row = 2;
            foreach (var dog in dogList.OrderBy(d => d.Name))
            {
                var hundefuehrer = personalList.FirstOrDefault(p => p.Id == dog.HundefuehrerId);
                
                ws.Cell(row, 1).Value = dog.Name;
                ws.Cell(row, 2).Value = dog.Rasse;
                ws.Cell(row, 3).Value = dog.Alter;
                ws.Cell(row, 4).Value = GetSpecializationsString(dog.Specializations);
                ws.Cell(row, 5).Value = hundefuehrer?.FullName ?? "";
                ws.Cell(row, 6).Value = dog.Notizen;
                ws.Cell(row, 7).Value = dog.IsActive ? "Ja" : "Nein";
                row++;
            }

            // Spaltenbreite automatisch anpassen
            ws.Columns().AdjustToContents();

            return Task.CompletedTask;
        }

        private Task ExportDrohnenSheet(XLWorkbook workbook, List<DroneEntry> droneList, List<PersonalEntry> personalList)
        {
            var ws = workbook.Worksheets.Add("Drohnen");

            // Header
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Hersteller";
            ws.Cell(1, 3).Value = "Modell";
            ws.Cell(1, 4).Value = "Seriennummer";
            ws.Cell(1, 5).Value = "Drohnenpilot";
            ws.Cell(1, 6).Value = "Notizen";
            ws.Cell(1, 7).Value = "Aktiv";

            // Header-Formatierung
            var headerRange = ws.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightCoral;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            // Daten
            int row = 2;
            foreach (var drone in droneList.OrderBy(d => d.Name))
            {
                var pilot = personalList.FirstOrDefault(p => p.Id == drone.DrohnenpilotId);
                
                ws.Cell(row, 1).Value = drone.Name;
                ws.Cell(row, 2).Value = drone.Hersteller;
                ws.Cell(row, 3).Value = drone.Modell;
                ws.Cell(row, 4).Value = drone.Seriennummer;
                ws.Cell(row, 5).Value = pilot?.FullName ?? "";
                ws.Cell(row, 6).Value = drone.Notizen;
                ws.Cell(row, 7).Value = drone.IsActive ? "Ja" : "Nein";
                row++;
            }

            // Spaltenbreite automatisch anpassen
            ws.Columns().AdjustToContents();

            return Task.CompletedTask;
        }

        public async Task<ImportResult> ImportStammdatenAsync(byte[] excelData)
        {
            var result = new ImportResult { Success = true };

            try
            {
                using var stream = new MemoryStream(excelData);
                using var workbook = new XLWorkbook(stream);

                // Zuerst Personal importieren (für Referenzen)
                var personalList = await _masterDataService.GetPersonalListAsync();
                
                if (workbook.Worksheets.TryGetWorksheet("Personal", out var personalSheet))
                {
                    await ImportPersonalSheet(personalSheet, result);
                    // Aktualisierte Liste laden für Referenzen
                    personalList = await _masterDataService.GetPersonalListAsync();
                }

                if (workbook.Worksheets.TryGetWorksheet("Hunde", out var hundeSheet))
                {
                    await ImportHundeSheet(hundeSheet, personalList, result);
                }

                if (workbook.Worksheets.TryGetWorksheet("Drohnen", out var drohnenSheet))
                {
                    await ImportDrohnenSheet(drohnenSheet, personalList, result);
                }

                result.Message = $"Import erfolgreich: {result.TotalImported} Einträge importiert" +
                                 (result.TotalSkipped > 0 ? $", {result.TotalSkipped} übersprungen" : "");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Fehler beim Import: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private async Task ImportPersonalSheet(IXLWorksheet ws, ImportResult result)
        {
            var existingPersonal = await _masterDataService.GetPersonalListAsync();
            var rows = ws.RowsUsed().Skip(1); // Header überspringen

            foreach (var row in rows)
            {
                try
                {
                    var vorname = row.Cell(1).GetString().Trim();
                    var nachname = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrEmpty(vorname) && string.IsNullOrEmpty(nachname))
                        continue; // Leere Zeile überspringen

                    // Prüfen ob Person bereits existiert
                    var existing = existingPersonal.FirstOrDefault(p => 
                        p.Vorname.Equals(vorname, StringComparison.OrdinalIgnoreCase) &&
                        p.Nachname.Equals(nachname, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        result.PersonalSkipped++;
                        result.Warnings.Add($"Personal '{vorname} {nachname}' existiert bereits und wurde übersprungen");
                        continue;
                    }

                    var person = new PersonalEntry
                    {
                        Vorname = vorname,
                        Nachname = nachname,
                        Skills = ParseSkills(row.Cell(3).GetString()),
                        Notizen = row.Cell(4).GetString().Trim(),
                        IsActive = ParseBool(row.Cell(5).GetString())
                    };

                    await _masterDataService.AddPersonalAsync(person);
                    result.PersonalImported++;
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Zeile {row.RowNumber()}: {ex.Message}");
                }
            }
        }

        private async Task ImportHundeSheet(IXLWorksheet ws, List<PersonalEntry> personalList, ImportResult result)
        {
            var existingDogs = await _masterDataService.GetDogListAsync();
            var rows = ws.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                try
                {
                    var name = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    // Prüfen ob Hund bereits existiert
                    var existing = existingDogs.FirstOrDefault(d => 
                        d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        result.HundeSkipped++;
                        result.Warnings.Add($"Hund '{name}' existiert bereits und wurde übersprungen");
                        continue;
                    }

                    var hundefuehrerName = row.Cell(5).GetString().Trim();
                    var hundefuehrer = FindPersonByName(personalList, hundefuehrerName);

                    var dog = new DogEntry
                    {
                        Name = name,
                        Rasse = row.Cell(2).GetString().Trim(),
                        Alter = ParseInt(row.Cell(3).GetString()),
                        Specializations = ParseSpecializations(row.Cell(4).GetString()),
                        HundefuehrerId = hundefuehrer?.Id ?? "",
                        Notizen = row.Cell(6).GetString().Trim(),
                        IsActive = ParseBool(row.Cell(7).GetString())
                    };

                    await _masterDataService.AddDogAsync(dog);
                    result.HundeImported++;

                    if (!string.IsNullOrEmpty(hundefuehrerName) && hundefuehrer == null)
                    {
                        result.Warnings.Add($"Hundeführer '{hundefuehrerName}' für Hund '{name}' nicht gefunden");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Hunde Zeile {row.RowNumber()}: {ex.Message}");
                }
            }
        }

        private async Task ImportDrohnenSheet(IXLWorksheet ws, List<PersonalEntry> personalList, ImportResult result)
        {
            var existingDrones = await _masterDataService.GetDroneListAsync();
            var rows = ws.RowsUsed().Skip(1);

            foreach (var row in rows)
            {
                try
                {
                    var name = row.Cell(1).GetString().Trim();
                    var seriennummer = row.Cell(4).GetString().Trim();
                    
                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(seriennummer))
                        continue;

                    // Prüfen ob Drohne bereits existiert (nach Name oder Seriennummer)
                    var existing = existingDrones.FirstOrDefault(d => 
                        (!string.IsNullOrEmpty(name) && d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(seriennummer) && d.Seriennummer.Equals(seriennummer, StringComparison.OrdinalIgnoreCase)));

                    if (existing != null)
                    {
                        result.DrohnenSkipped++;
                        result.Warnings.Add($"Drohne '{name}' existiert bereits und wurde übersprungen");
                        continue;
                    }

                    var pilotName = row.Cell(5).GetString().Trim();
                    var pilot = FindPersonByName(personalList, pilotName);

                    var drone = new DroneEntry
                    {
                        Name = name,
                        Hersteller = row.Cell(2).GetString().Trim(),
                        Modell = row.Cell(3).GetString().Trim(),
                        Seriennummer = seriennummer,
                        DrohnenpilotId = pilot?.Id ?? "",
                        Notizen = row.Cell(6).GetString().Trim(),
                        IsActive = ParseBool(row.Cell(7).GetString())
                    };

                    await _masterDataService.AddDroneAsync(drone);
                    result.DrohnenImported++;

                    if (!string.IsNullOrEmpty(pilotName) && pilot == null)
                    {
                        result.Warnings.Add($"Drohnenpilot '{pilotName}' für Drohne '{name}' nicht gefunden");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Drohnen Zeile {row.RowNumber()}: {ex.Message}");
                }
            }
        }

        public Task<byte[]> CreateImportTemplateAsync()
        {
            using var workbook = new XLWorkbook();

            // Personal-Vorlage
            var wsPersonal = workbook.Worksheets.Add("Personal");
            wsPersonal.Cell(1, 1).Value = "Vorname";
            wsPersonal.Cell(1, 2).Value = "Nachname";
            wsPersonal.Cell(1, 3).Value = "Qualifikationen";
            wsPersonal.Cell(1, 4).Value = "Notizen";
            wsPersonal.Cell(1, 5).Value = "Aktiv";

            var personalHeader = wsPersonal.Range(1, 1, 1, 5);
            personalHeader.Style.Font.Bold = true;
            personalHeader.Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Beispielzeile
            wsPersonal.Cell(2, 1).Value = "Max";
            wsPersonal.Cell(2, 2).Value = "Mustermann";
            wsPersonal.Cell(2, 3).Value = "Hundeführer, Helfer";
            wsPersonal.Cell(2, 4).Value = "Beispiel-Notiz";
            wsPersonal.Cell(2, 5).Value = "Ja";

            // Hinweis zu Qualifikationen
            wsPersonal.Cell(4, 1).Value = "Mögliche Qualifikationen:";
            wsPersonal.Cell(4, 1).Style.Font.Bold = true;
            wsPersonal.Cell(5, 1).Value = "Hundeführer, Helfer, Führungsassistent, Gruppenführer, Zugführer, Verbandsführer, Drohnenpilot, Einsatzleiter";
            
            wsPersonal.Columns().AdjustToContents();

            // Hunde-Vorlage
            var wsHunde = workbook.Worksheets.Add("Hunde");
            wsHunde.Cell(1, 1).Value = "Name";
            wsHunde.Cell(1, 2).Value = "Rasse";
            wsHunde.Cell(1, 3).Value = "Alter";
            wsHunde.Cell(1, 4).Value = "Spezialisierungen";
            wsHunde.Cell(1, 5).Value = "Hundeführer";
            wsHunde.Cell(1, 6).Value = "Notizen";
            wsHunde.Cell(1, 7).Value = "Aktiv";

            var hundeHeader = wsHunde.Range(1, 1, 1, 7);
            hundeHeader.Style.Font.Bold = true;
            hundeHeader.Style.Fill.BackgroundColor = XLColor.LightGreen;

            // Beispielzeile
            wsHunde.Cell(2, 1).Value = "Rex";
            wsHunde.Cell(2, 2).Value = "Schäferhund";
            wsHunde.Cell(2, 3).Value = "5";
            wsHunde.Cell(2, 4).Value = "Flächensuche, Trümmersuche";
            wsHunde.Cell(2, 5).Value = "Max Mustermann";
            wsHunde.Cell(2, 6).Value = "Beispiel-Notiz";
            wsHunde.Cell(2, 7).Value = "Ja";

            // Hinweis zu Spezialisierungen
            wsHunde.Cell(4, 1).Value = "Mögliche Spezialisierungen:";
            wsHunde.Cell(4, 1).Style.Font.Bold = true;
            wsHunde.Cell(5, 1).Value = "Flächensuche, Trümmersuche, Wasserortung, Mantrailing, Lawinensuche";

            wsHunde.Columns().AdjustToContents();

            // Drohnen-Vorlage
            var wsDrohnen = workbook.Worksheets.Add("Drohnen");
            wsDrohnen.Cell(1, 1).Value = "Name";
            wsDrohnen.Cell(1, 2).Value = "Hersteller";
            wsDrohnen.Cell(1, 3).Value = "Modell";
            wsDrohnen.Cell(1, 4).Value = "Seriennummer";
            wsDrohnen.Cell(1, 5).Value = "Drohnenpilot";
            wsDrohnen.Cell(1, 6).Value = "Notizen";
            wsDrohnen.Cell(1, 7).Value = "Aktiv";

            var drohnenHeader = wsDrohnen.Range(1, 1, 1, 7);
            drohnenHeader.Style.Font.Bold = true;
            drohnenHeader.Style.Fill.BackgroundColor = XLColor.LightCoral;

            // Beispielzeile
            wsDrohnen.Cell(2, 1).Value = "Drohne 1";
            wsDrohnen.Cell(2, 2).Value = "DJI";
            wsDrohnen.Cell(2, 3).Value = "Mavic 3";
            wsDrohnen.Cell(2, 4).Value = "SN123456789";
            wsDrohnen.Cell(2, 5).Value = "Max Mustermann";
            wsDrohnen.Cell(2, 6).Value = "Beispiel-Notiz";
            wsDrohnen.Cell(2, 7).Value = "Ja";

            wsDrohnen.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(stream.ToArray());
        }

        #region Helper Methods

        private string GetSkillsString(PersonalSkills skills)
        {
            if (skills == PersonalSkills.None)
                return "";

            var skillList = new List<string>();
            foreach (PersonalSkills skill in Enum.GetValues(typeof(PersonalSkills)))
            {
                if (skill != PersonalSkills.None && skills.HasFlag(skill))
                {
                    skillList.Add(skill.GetDisplayName());
                }
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
                {
                    specList.Add(spec.GetDisplayName());
                }
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

            // Versuche exakte Übereinstimmung mit FullName
            var exact = personalList.FirstOrDefault(p => 
                p.FullName.Equals(fullName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            // Versuche mit Vorname + Nachname
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

            // Versuche teilweise Übereinstimmung
            return personalList.FirstOrDefault(p =>
                p.FullName.Contains(fullName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                fullName.Trim().Contains(p.FullName, StringComparison.OrdinalIgnoreCase));
        }

        private bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true; // Standard: aktiv

            var lower = value.Trim().ToLowerInvariant();
            return lower == "ja" || lower == "yes" || lower == "true" || lower == "1" || lower == "aktiv" || lower == "x";
        }

        private int ParseInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return int.TryParse(value.Trim(), out var result) ? result : 0;
        }

        #endregion
    }
}
