using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzService
    {
        public Task AddGlobalNoteAsync(string text, GlobalNotesEntryType type = GlobalNotesEntryType.Manual, string teamId = "")
        {
            var note = new GlobalNotesEntry
            {
                Text = text,
                Type = type,
                SourceTeamId = teamId,
                Timestamp = _timeService?.Now ?? DateTime.Now
            };

            if (!string.IsNullOrWhiteSpace(teamId))
            {
                var team = _teams.FirstOrDefault(t => t.TeamId == teamId);
                if (team != null)
                    note.SourceTeamName = team.TeamName;
            }

            _globalNotes.Add(note);
            NoteAdded?.Invoke(note);
            return Task.CompletedTask;
        }

        public Task<List<GlobalNotesEntry>> GetFilteredNotesAsync(string? teamId = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return Task.FromResult(_globalNotes.OrderByDescending(n => n.Timestamp).ToList());

            var filtered = _globalNotes
                .Where(n => string.IsNullOrWhiteSpace(n.SourceTeamId) || n.SourceTeamId == teamId)
                .OrderByDescending(n => n.Timestamp)
                .ToList();
            return Task.FromResult(filtered);
        }

        public Task RemoveGlobalNoteAsync(string noteId)
        {
            var note = _globalNotes.FirstOrDefault(n => n.Id == noteId);
            if (note != null)
            {
                _globalNotes.Remove(note);
                _currentEinsatz.GlobalNotesEntries.Remove(note);
            }
            return Task.CompletedTask;
        }

        public Task<GlobalNotesEntry> AddGlobalNoteWithSourceAsync(
            string text,
            string sourceTeamId,
            string sourceTeamName,
            string sourceType,
            GlobalNotesEntryType type = GlobalNotesEntryType.Manual,
            string createdBy = "System")
        {
            var note = new GlobalNotesEntry
            {
                Text = text,
                Type = type,
                SourceTeamId = sourceTeamId,
                SourceTeamName = sourceTeamName,
                SourceType = sourceType,
                Timestamp = _timeService?.Now ?? DateTime.Now,
                CreatedBy = createdBy,
                Replies = new List<GlobalNotesReply>()
            };

            _globalNotes.Add(note);
            _currentEinsatz.GlobalNotesEntries.Add(note);
            NoteAdded?.Invoke(note);
            return Task.FromResult(note);
        }

        public Task<GlobalNotesEntry> UpdateGlobalNoteAsync(string noteId, string newText, string updatedBy = "System")
        {
            var note = _globalNotes.FirstOrDefault(n => n.Id == noteId)
                ?? throw new InvalidOperationException($"Notiz mit ID {noteId} nicht gefunden");

            _noteHistory.Add(new GlobalNotesHistory
            {
                NoteId = noteId,
                OldText = note.Text,
                NewText = newText,
                ChangedAt = _timeService?.Now ?? DateTime.Now,
                ChangedBy = updatedBy
            });

            note.Text = newText;
            note.UpdatedAt = _timeService?.Now ?? DateTime.Now;
            note.UpdatedBy = updatedBy;

            EinsatzChanged?.Invoke();
            return Task.FromResult(note);
        }

        public Task<GlobalNotesEntry?> GetGlobalNoteByIdAsync(string noteId)
            => Task.FromResult(_globalNotes.FirstOrDefault(n => n.Id == noteId));

        public Task<List<GlobalNotesHistory>> GetNoteHistoryAsync(string noteId)
        {
            var history = _noteHistory
                .Where(h => h.NoteId == noteId)
                .OrderByDescending(h => h.ChangedAt)
                .ToList();
            return Task.FromResult(history);
        }

        public Task<GlobalNotesReply> AddReplyToNoteAsync(
            string noteId,
            string text,
            string sourceTeamId,
            string sourceTeamName,
            string createdBy = "System")
        {
            var note = _globalNotes.FirstOrDefault(n => n.Id == noteId)
                ?? throw new InvalidOperationException($"Notiz mit ID {noteId} nicht gefunden");

            var reply = new GlobalNotesReply
            {
                NoteId = noteId,
                Text = text,
                SourceTeamId = sourceTeamId,
                SourceTeamName = sourceTeamName,
                Timestamp = _timeService?.Now ?? DateTime.Now,
                CreatedBy = createdBy
            };

            note.Replies.Add(reply);
            EinsatzChanged?.Invoke();
            return Task.FromResult(reply);
        }

        public Task<GlobalNotesReply> UpdateReplyAsync(string replyId, string newText, string updatedBy = "System")
        {
            GlobalNotesReply? reply = null;
            foreach (var note in _globalNotes)
            {
                reply = note.Replies.FirstOrDefault(r => r.Id == replyId);
                if (reply != null) break;
            }

            if (reply == null)
                throw new InvalidOperationException($"Antwort mit ID {replyId} nicht gefunden");

            reply.Text = newText;
            reply.UpdatedAt = _timeService?.Now ?? DateTime.Now;
            reply.UpdatedBy = updatedBy;

            EinsatzChanged?.Invoke();
            return Task.FromResult(reply);
        }

        public Task DeleteReplyAsync(string replyId)
        {
            foreach (var note in _globalNotes)
            {
                var reply = note.Replies.FirstOrDefault(r => r.Id == replyId);
                if (reply != null)
                {
                    note.Replies.Remove(reply);
                    EinsatzChanged?.Invoke();
                    break;
                }
            }
            return Task.CompletedTask;
        }

        public Task<List<GlobalNotesReply>> GetRepliesForNoteAsync(string noteId)
        {
            var note = _globalNotes.FirstOrDefault(n => n.Id == noteId);
            if (note == null)
                return Task.FromResult(new List<GlobalNotesReply>());
            return Task.FromResult(note.Replies.OrderBy(r => r.Timestamp).ToList());
        }

        public Task UpdateVermisstenInfoAsync(VermisstenInfo info)
        {
            info.ZuletztAktualisiert = _timeService?.Now ?? DateTime.Now;
            _currentEinsatz.VermisstenInfo = info;
            VermisstenInfoChanged?.Invoke();
            EinsatzChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task AddElNotizAsync(string text, string prefix = "")
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.CompletedTask;

            var entry = new ElNotizEntry
            {
                Text = text.Trim(),
                Prefix = prefix,
                Timestamp = _timeService?.Now ?? DateTime.Now
            };
            _currentEinsatz.ElNotizen ??= new List<ElNotizEntry>();
            _currentEinsatz.ElNotizen.Insert(0, entry);
            ElNotizAdded?.Invoke();
            return Task.CompletedTask;
        }

        public Task DeleteElNotizAsync(string notizId)
        {
            _currentEinsatz.ElNotizen ??= new List<ElNotizEntry>();
            var entry = _currentEinsatz.ElNotizen.FirstOrDefault(n => n.Id == notizId);
            if (entry != null)
                _currentEinsatz.ElNotizen.Remove(entry);
            return Task.CompletedTask;
        }
    }
}
