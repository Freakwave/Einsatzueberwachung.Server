// PDF-Export-Service mit QuestPDF
// Erstellt professionelle PDF-Berichte für Einsätze

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Einsatzueberwachung.Domain.Services
{
    public class PdfExportService : IPdfExportService
    {
        public PdfExportService()
        {
            // QuestPDF Lizenz-Konfiguration (Community License für nicht-kommerzielle Nutzung)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<PdfExportResult> ExportEinsatzToPdfAsync(EinsatzData einsatzData, List<Team> teams, List<GlobalNotesEntry> notes)
        {
            try
            {
                var filename = $"Einsatzbericht_{einsatzData.EinsatzNummer}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var einsatzPath = Path.Combine(documentsPath, "Einsatzueberwachung", "Berichte");
                
                if (!Directory.Exists(einsatzPath))
                {
                    Directory.CreateDirectory(einsatzPath);
                }

                var filePath = Path.Combine(einsatzPath, filename);

                await Task.Run(() =>
                {
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(2, Unit.Centimetre);
                            page.PageColor(Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(11));

                            page.Header()
                                .Height(100)
                                .Background(Colors.Blue.Lighten3)
                                .Padding(20)
                                .Column(column =>
                                {
                                    column.Item().Text("EINSATZBERICHT")
                                        .FontSize(24)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken2);

                                    if (!string.IsNullOrEmpty(einsatzData.StaffelName))
                                    {
                                        column.Item().Text(einsatzData.StaffelName)
                                            .FontSize(14)
                                            .FontColor(Colors.Grey.Darken1);
                                    }
                                    
                                    column.Item().Text($"Rettungshundestaffel")
                                        .FontSize(12)
                                        .FontColor(Colors.Grey.Darken1);
                                });

                            page.Content()
                                .Column(column =>
                                {
                                    // Grunddaten
                                    column.Item().PaddingVertical(10).Element(container => ComposeGrunddaten(container, einsatzData));

                                    // Teams
                                    column.Item().PaddingVertical(10).Element(container => ComposeTeams(container, teams));

                                    // Suchgebiete
                                    if (einsatzData.SearchAreas.Any())
                                    {
                                        column.Item().PaddingVertical(10).Element(container => ComposeSuchgebiete(container, einsatzData.SearchAreas.ToList()));
                                    }

                                    // Notizen
                                    column.Item().PageBreak();
                                    column.Item().PaddingVertical(10).Element(container => ComposeNotizen(container, notes));
                                });

                            page.Footer()
                                .AlignCenter()
                                .Text(text =>
                                {
                                    text.Span("Erstellt am: ");
                                    text.Span($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}").Bold();
                                    text.Span(" | Seite ");
                                    text.CurrentPageNumber();
                                    text.Span(" von ");
                                    text.TotalPages();
                                });
                        });
                    })
                    .GeneratePdf(filePath);
                });

                return new PdfExportResult
                {
                    Success = true,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                return new PdfExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private void ComposeGrunddaten(IContainer container, EinsatzData einsatzData)
        {
            container.Column(column =>
            {
                column.Item().Text("Grunddaten")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150);
                        columns.RelativeColumn();
                    });

                    AddTableRow(table, "Einsatznummer:", einsatzData.EinsatzNummer);
                    AddTableRow(table, "Einsatztyp:", einsatzData.EinsatzTyp);
                    AddTableRow(table, "Datum:", einsatzData.EinsatzDatum.ToString("dd.MM.yyyy HH:mm"));
                    
                    if (einsatzData.AlarmierungsZeit.HasValue)
                    {
                        AddTableRow(table, "Alarmierung:", einsatzData.AlarmierungsZeit.Value.ToString("dd.MM.yyyy HH:mm"));
                    }
                    
                    AddTableRow(table, "Einsatzort:", einsatzData.Einsatzort);
                    AddTableRow(table, "Alarmiert durch:", einsatzData.Alarmiert);
                    AddTableRow(table, "Einsatzleiter:", einsatzData.Einsatzleiter);
                    AddTableRow(table, "Führungsassistent:", einsatzData.Fuehrungsassistent);
                });
            });
        }

        private void ComposeTeams(IContainer container, List<Team> teams)
        {
            container.Column(column =>
            {
                column.Item().Text($"Teams ({teams.Count})")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Team").Bold();
                        header.Cell().Element(CellStyle).Text("Hund/Drohne").Bold();
                        header.Cell().Element(CellStyle).Text("Personal").Bold();
                        header.Cell().Element(CellStyle).Text("Suchgebiet").Bold();
                        header.Cell().Element(CellStyle).Text("Einsatzzeit").Bold();

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.Background(Colors.Blue.Lighten3).Padding(5);
                        }
                    });

                    // Rows
                    foreach (var team in teams)
                    {
                        table.Cell().Element(CellStyle).Text(team.TeamName);
                        
                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            if (team.IsDroneTeam)
                            {
                                col.Item().Text($"Drohne: {team.DroneType}").FontSize(9);
                            }
                            else if (!string.IsNullOrEmpty(team.DogName))
                            {
                                col.Item().Text(team.DogName).FontSize(9);
                                col.Item().Text($"({team.DogSpecialization.GetShortName()})").FontSize(8).Italic();
                            }
                            else
                            {
                                col.Item().Text("Support").FontSize(9).Italic();
                            }
                        });

                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            col.Item().Text(team.HundefuehrerName).FontSize(9);
                            if (!string.IsNullOrEmpty(team.HelferName))
                            {
                                col.Item().Text($"Helfer: {team.HelferName}").FontSize(8).Italic();
                            }
                        });

                        table.Cell().Element(CellStyle).Text(team.SearchAreaName ?? "-").FontSize(9);
                        
                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            col.Item().Text(FormatTimeSpan(team.ElapsedTime)).FontSize(9);
                            if (team.IsSecondWarning)
                            {
                                col.Item().Text("KRITISCH").FontSize(8).Bold().FontColor(Colors.Red.Medium);
                            }
                            else if (team.IsFirstWarning)
                            {
                                col.Item().Text("Warnung").FontSize(8).FontColor(Colors.Orange.Medium);
                            }
                        });

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                        }
                    }
                });
            });
        }

        private void ComposeSuchgebiete(IContainer container, List<SearchArea> searchAreas)
        {
            container.Column(column =>
            {
                column.Item().Text($"Suchgebiete ({searchAreas.Count})")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(3);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Name").Bold();
                        header.Cell().Element(CellStyle).Text("Team").Bold();
                        header.Cell().Element(CellStyle).Text("Status").Bold();
                        header.Cell().Element(CellStyle).Text("Notizen").Bold();

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.Background(Colors.Blue.Lighten3).Padding(5);
                        }
                    });

                    foreach (var area in searchAreas)
                    {
                        table.Cell().Element(CellStyle).Text(area.Name);
                        table.Cell().Element(CellStyle).Text(area.AssignedTeamName ?? "-");
                        table.Cell().Element(CellStyle).Text(area.IsCompleted ? "Abgeschlossen" : "In Bearbeitung")
                            .FontColor(area.IsCompleted ? Colors.Green.Medium : Colors.Orange.Medium);
                        table.Cell().Element(CellStyle).Text(area.Notes ?? "-").FontSize(9);

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                        }
                    }
                });
            });
        }

        private void ComposeNotizen(IContainer container, List<GlobalNotesEntry> notes)
        {
            container.Column(column =>
            {
                column.Item().Text($"Funksprüche & Notizen ({notes.Count})")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Column(col =>
                {
                    foreach (var note in notes.OrderBy(n => n.Timestamp))
                    {
                        col.Item().PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(noteCol =>
                        {
                            noteCol.Item().Row(row =>
                            {
                                row.AutoItem().Text($"[{note.Timestamp:dd.MM.yyyy HH:mm:ss}]")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(Colors.Grey.Darken1);

                                row.AutoItem().PaddingLeft(10).Text($"[{note.Type}]")
                                    .FontSize(9)
                                    .Bold()
                                    .FontColor(GetNoteTypeColor(note.Type));

                                if (!string.IsNullOrEmpty(note.SourceTeamName))
                                {
                                    row.AutoItem().PaddingLeft(10).Text($"[{note.SourceTeamName}]")
                                        .FontSize(9)
                                        .FontColor(Colors.Blue.Medium);
                                }
                            });

                            noteCol.Item().PaddingTop(3).Text(note.Text).FontSize(10);
                        });
                    }
                });
            });
        }

        private void AddTableRow(TableDescriptor table, string label, string value)
        {
            table.Cell().Text(label).Bold();
            table.Cell().Text(value ?? "-");
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private string GetNoteTypeColor(GlobalNotesEntryType type)
        {
            return type switch
            {
                GlobalNotesEntryType.TeamStart => Colors.Green.Medium,
                GlobalNotesEntryType.TeamStop => Colors.Orange.Medium,
                GlobalNotesEntryType.TeamWarning => Colors.Red.Medium,
                GlobalNotesEntryType.TeamReset => Colors.Blue.Medium,
                GlobalNotesEntryType.EinsatzUpdate => Colors.Blue.Darken1,
                GlobalNotesEntryType.System => Colors.Grey.Darken1,
                _ => Colors.Grey.Darken1
            };
        }

        /// <summary>
        /// Exportiert einen archivierten Einsatz als PDF (speichert Datei)
        /// </summary>
        public async Task<PdfExportResult> ExportArchivedEinsatzToPdfAsync(ArchivedEinsatz archivedEinsatz)
        {
            try
            {
                var filename = $"Einsatzbericht_{archivedEinsatz.EinsatzNummer}_{archivedEinsatz.EinsatzDatum:yyyyMMdd}.pdf";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var einsatzPath = Path.Combine(documentsPath, "Einsatzueberwachung", "Berichte");
                
                if (!Directory.Exists(einsatzPath))
                {
                    Directory.CreateDirectory(einsatzPath);
                }

                var filePath = Path.Combine(einsatzPath, filename);
                var pdfDocument = CreateArchivedEinsatzDocument(archivedEinsatz);

                await Task.Run(() =>
                {
                    pdfDocument.GeneratePdf(filePath);
                });

                return new PdfExportResult
                {
                    Success = true,
                    FilePath = filePath
                };
            }
            catch (Exception ex)
            {
                return new PdfExportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Exportiert einen archivierten Einsatz als PDF-Byte-Array (für Browser-Download)
        /// </summary>
        public async Task<byte[]> ExportArchivedEinsatzToPdfBytesAsync(ArchivedEinsatz archivedEinsatz)
        {
            var pdfDocument = CreateArchivedEinsatzDocument(archivedEinsatz);
            
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream();
                pdfDocument.GeneratePdf(stream);
                return stream.ToArray();
            });
        }

        /// <summary>
        /// Exportiert einen aktiven Einsatz als PDF-Byte-Array (für Browser-Download)
        /// </summary>
        public async Task<byte[]> ExportEinsatzToPdfBytesAsync(EinsatzData einsatzData, List<Team> teams, List<GlobalNotesEntry> notes)
        {
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream();
                
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header()
                            .Height(100)
                            .Background(Colors.Blue.Lighten3)
                            .Padding(20)
                            .Column(column =>
                            {
                                column.Item().Text("EINSATZBERICHT")
                                    .FontSize(24)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken2);

                                if (!string.IsNullOrEmpty(einsatzData.StaffelName))
                                {
                                    column.Item().Text(einsatzData.StaffelName)
                                        .FontSize(14)
                                        .FontColor(Colors.Grey.Darken1);
                                }
                                
                                column.Item().Text($"Rettungshundestaffel")
                                    .FontSize(12)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                        page.Content()
                            .Column(column =>
                            {
                                column.Item().PaddingVertical(10).Element(container => ComposeGrunddaten(container, einsatzData));
                                column.Item().PaddingVertical(10).Element(container => ComposeTeams(container, teams));
                                
                                if (einsatzData.SearchAreas.Any())
                                {
                                    column.Item().PaddingVertical(10).Element(container => ComposeSuchgebiete(container, einsatzData.SearchAreas.ToList()));
                                }

                                column.Item().PageBreak();
                                column.Item().PaddingVertical(10).Element(container => ComposeNotizen(container, notes));
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Erstellt am: ");
                                text.Span($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}").Bold();
                                text.Span(" | Seite ");
                                text.CurrentPageNumber();
                                text.Span(" von ");
                                text.TotalPages();
                            });
                    });
                })
                .GeneratePdf(stream);
                
                return stream.ToArray();
            });
        }

        /// <summary>
        /// Erstellt das PDF-Dokument für einen archivierten Einsatz
        /// </summary>
        private Document CreateArchivedEinsatzDocument(ArchivedEinsatz einsatz)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Height(100)
                        .Background(einsatz.IstEinsatz ? Colors.Red.Lighten3 : Colors.Blue.Lighten3)
                        .Padding(20)
                        .Column(column =>
                        {
                            column.Item().Text(einsatz.IstEinsatz ? "EINSATZBERICHT" : "ÜBUNGSBERICHT")
                                .FontSize(24)
                                .Bold()
                                .FontColor(einsatz.IstEinsatz ? Colors.Red.Darken2 : Colors.Blue.Darken2);

                            if (!string.IsNullOrEmpty(einsatz.StaffelName))
                            {
                                column.Item().Text(einsatz.StaffelName)
                                    .FontSize(14)
                                    .FontColor(Colors.Grey.Darken1);
                            }
                            
                            column.Item().Text("Rettungshundestaffel")
                                .FontSize(12)
                                .FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .Column(column =>
                        {
                            // Grunddaten
                            column.Item().PaddingVertical(10).Element(c => ComposeArchivedGrunddaten(c, einsatz));

                            // Ergebnis
                            column.Item().PaddingVertical(10).Element(c => ComposeErgebnis(c, einsatz));

                            // Teams
                            if (einsatz.Teams?.Any() == true)
                            {
                                column.Item().PaddingVertical(10).Element(c => ComposeArchivedTeams(c, einsatz.Teams));
                            }

                            // Suchgebiete
                            if (einsatz.SearchAreas?.Any() == true)
                            {
                                column.Item().PaddingVertical(10).Element(c => ComposeSuchgebiete(c, einsatz.SearchAreas));
                            }

                            // Notizen
                            if (einsatz.GlobalNotesEntries?.Any() == true)
                            {
                                column.Item().PageBreak();
                                column.Item().PaddingVertical(10).Element(c => ComposeNotizen(c, einsatz.GlobalNotesEntries));
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Archiviert am: ");
                            text.Span($"{einsatz.ArchivedAt:dd.MM.yyyy HH:mm}").Bold();
                            text.Span(" | Erstellt am: ");
                            text.Span($"{DateTime.Now:dd.MM.yyyy HH:mm}");
                            text.Span(" | Seite ");
                            text.CurrentPageNumber();
                            text.Span(" von ");
                            text.TotalPages();
                        });
                });
            });
        }

        /// <summary>
        /// Komponiert die archivierten Teams
        /// </summary>
        private void ComposeArchivedTeams(IContainer container, List<ArchivedTeam> teams)
        {
            container.Column(column =>
            {
                column.Item().Text($"Teams ({teams.Count})")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Team").Bold();
                        header.Cell().Element(CellStyle).Text("Hund/Drohne").Bold();
                        header.Cell().Element(CellStyle).Text("Personal").Bold();
                        header.Cell().Element(CellStyle).Text("Status").Bold();

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.Background(Colors.Blue.Lighten3).Padding(5);
                        }
                    });

                    // Rows
                    foreach (var team in teams)
                    {
                        table.Cell().Element(CellStyle).Text(team.TeamName);
                        
                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            if (!string.IsNullOrEmpty(team.DroneName))
                            {
                                col.Item().Text($"Drohne: {team.DroneName}").FontSize(9);
                            }
                            else if (!string.IsNullOrEmpty(team.DogName))
                            {
                                col.Item().Text(team.DogName).FontSize(9);
                            }
                            else
                            {
                                col.Item().Text("-").FontSize(9);
                            }
                        });

                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            foreach (var member in team.MemberNames)
                            {
                                col.Item().Text(member).FontSize(9);
                            }
                            if (!team.MemberNames.Any())
                            {
                                col.Item().Text("-").FontSize(9);
                            }
                        });

                        table.Cell().Element(CellStyle).Column(col =>
                        {
                            col.Item().Text(team.Status).FontSize(9);
                            if (team.AusrueckZeit.HasValue)
                            {
                                col.Item().Text($"Ausrück: {team.AusrueckZeit.Value:HH:mm}").FontSize(8).Italic();
                            }
                            if (team.EinrueckZeit.HasValue)
                            {
                                col.Item().Text($"Einrück: {team.EinrueckZeit.Value:HH:mm}").FontSize(8).Italic();
                            }
                        });

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                        }
                    }
                });
            });
        }

        /// <summary>
        /// Komponiert die Grunddaten eines archivierten Einsatzes
        /// </summary>
        private void ComposeArchivedGrunddaten(IContainer container, ArchivedEinsatz einsatz)
        {
            container.Column(column =>
            {
                column.Item().Text("Grunddaten")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150);
                        columns.RelativeColumn();
                    });

                    AddTableRow(table, "Einsatznummer:", einsatz.EinsatzNummer);
                    AddTableRow(table, "Einsatztyp:", einsatz.EinsatzTyp);
                    AddTableRow(table, "Datum:", einsatz.EinsatzDatum.ToString("dd.MM.yyyy"));
                    
                    if (einsatz.AlarmierungsZeit.HasValue)
                    {
                        AddTableRow(table, "Alarmierung:", einsatz.AlarmierungsZeit.Value.ToString("HH:mm"));
                    }
                    
                    if (einsatz.EinsatzEnde.HasValue)
                    {
                        AddTableRow(table, "Einsatzende:", einsatz.EinsatzEnde.Value.ToString("HH:mm"));
                    }
                    
                    AddTableRow(table, "Dauer:", einsatz.DauerFormatiert);
                    AddTableRow(table, "Einsatzort:", einsatz.Einsatzort);
                    AddTableRow(table, "Alarmiert durch:", einsatz.Alarmiert);
                    AddTableRow(table, "Einsatzleiter:", einsatz.Einsatzleiter);
                    AddTableRow(table, "Führungsassistent:", einsatz.Fuehrungsassistent);
                });

                // Statistiken
                column.Item().PaddingTop(15).Row(row =>
                {
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().AlignCenter().Text($"{einsatz.AnzahlTeams}").FontSize(24).Bold();
                        col.Item().AlignCenter().Text("Teams").FontSize(10);
                    });
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().AlignCenter().Text($"{einsatz.AnzahlPersonal}").FontSize(24).Bold();
                        col.Item().AlignCenter().Text("Personal").FontSize(10);
                    });
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().AlignCenter().Text($"{einsatz.AnzahlHunde}").FontSize(24).Bold();
                        col.Item().AlignCenter().Text("Hunde").FontSize(10);
                    });
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().AlignCenter().Text($"{einsatz.AnzahlDrohnen}").FontSize(24).Bold();
                        col.Item().AlignCenter().Text("Drohnen").FontSize(10);
                    });
                });
            });
        }

        /// <summary>
        /// Komponiert das Ergebnis eines archivierten Einsatzes
        /// </summary>
        private void ComposeErgebnis(IContainer container, ArchivedEinsatz einsatz)
        {
            var ergebnisLower = (einsatz.Ergebnis ?? "").ToLowerInvariant();
            var bgColor = Colors.Grey.Lighten3;
            var textColor = Colors.Grey.Darken2;
            
            if (ergebnisLower.Contains("gefunden") || ergebnisLower.Contains("erfolg"))
            {
                bgColor = Colors.Green.Lighten3;
                textColor = Colors.Green.Darken2;
            }
            else if (ergebnisLower.Contains("abgebrochen") || ergebnisLower.Contains("erfolglos"))
            {
                bgColor = Colors.Orange.Lighten3;
                textColor = Colors.Orange.Darken2;
            }

            container.Column(column =>
            {
                column.Item().Text("Ergebnis")
                    .FontSize(16)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().PaddingTop(10).Background(bgColor).Padding(15).Column(col =>
                {
                    col.Item().Text(einsatz.Ergebnis ?? "Kein Ergebnis angegeben")
                        .FontSize(14)
                        .Bold()
                        .FontColor(textColor);
                    
                    if (!string.IsNullOrEmpty(einsatz.Bemerkungen))
                    {
                        col.Item().PaddingTop(10).Text("Bemerkungen:")
                            .FontSize(10)
                            .Bold();
                        col.Item().Text(einsatz.Bemerkungen)
                            .FontSize(10);
                    }
                });
            });
        }
    }
}
