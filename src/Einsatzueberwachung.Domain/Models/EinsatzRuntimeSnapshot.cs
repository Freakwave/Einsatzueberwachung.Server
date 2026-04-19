using System.Collections.Generic;

namespace Einsatzueberwachung.Domain.Models;

public class EinsatzRuntimeSnapshot
{
    public EinsatzData CurrentEinsatz { get; set; } = new();
    public List<Team> Teams { get; set; } = new();
    public List<GlobalNotesEntry> GlobalNotes { get; set; } = new();
    public List<GlobalNotesHistory> NoteHistory { get; set; } = new();

    /// <summary>
    /// Persistierte hundebezogene Pausendatensätze. Wird beim Import gegenüber der
    /// Rekonstruktion aus Team-Zuständen bevorzugt (deckt auch abgeschlossene Pausen ab,
    /// deren IsPausing inzwischen false ist).
    /// </summary>
    public List<DogPauseRecord> DogPauses { get; set; } = new();
}
