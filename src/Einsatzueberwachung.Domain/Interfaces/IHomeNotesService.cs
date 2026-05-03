using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public record HomeNoteEntry(string Id, string Text, DateTime CreatedAt);

    public interface IHomeNotesService
    {
        Task<List<HomeNoteEntry>> GetNotesAsync();
        Task AddNoteAsync(string text);
        Task DeleteNoteAsync(string id);
    }
}
