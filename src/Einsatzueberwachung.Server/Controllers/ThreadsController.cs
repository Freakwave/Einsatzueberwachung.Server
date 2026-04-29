using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Models.Enums;
using Einsatzueberwachung.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace Einsatzueberwachung.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ThreadsController(IEinsatzService einsatzService) : ControllerBase
{
    [HttpGet]
    public IActionResult GetNotes([FromQuery] string filter = "alle")
    {
        IEnumerable<GlobalNotesEntry> query = einsatzService.GlobalNotes;
        var normalized = filter.Trim().ToLowerInvariant();

        if (normalized == "funk")
        {
            query = query.Where(n => string.Equals(n.SourceType, "Funk", StringComparison.OrdinalIgnoreCase));
        }
        else if (normalized == "notiz")
        {
            query = query.Where(n => !string.Equals(n.SourceType, "Funk", StringComparison.OrdinalIgnoreCase));
        }

        var notes = query
            .OrderByDescending(n => n.Timestamp)
            .Take(200)
            .Select(ToNoteDto)
            .ToList();

        return Ok(new { notes, count = notes.Count });
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new ErrorResponse("Text darf nicht leer sein."));
        }

        var sourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Notiz" : request.SourceType.Trim();
        var note = await einsatzService.AddGlobalNoteWithSourceAsync(
            request.Text.Trim(),
            string.IsNullOrWhiteSpace(request.SourceTeamId) ? "mobile" : request.SourceTeamId.Trim(),
            string.IsNullOrWhiteSpace(request.SourceTeamName) ? "Mobile" : request.SourceTeamName.Trim(),
            sourceType,
            ParseNoteType(sourceType),
            string.IsNullOrWhiteSpace(request.CreatedBy) ? "Mobile" : request.CreatedBy.Trim());

        return CreatedAtAction(nameof(GetNotes), new { filter = "alle" }, ToNoteDto(note));
    }

    [HttpPost("{noteId}/replies")]
    public async Task<IActionResult> AddReply(string noteId, [FromBody] CreateReplyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new ErrorResponse("Antworttext darf nicht leer sein."));
        }

        try
        {
            var reply = await einsatzService.AddReplyToNoteAsync(
                noteId,
                request.Text.Trim(),
                string.IsNullOrWhiteSpace(request.SourceTeamId) ? "mobile" : request.SourceTeamId.Trim(),
                string.IsNullOrWhiteSpace(request.SourceTeamName) ? "Mobile" : request.SourceTeamName.Trim(),
                string.IsNullOrWhiteSpace(request.CreatedBy) ? "Mobile" : request.CreatedBy.Trim());

            return Ok(new { message = "Antwort hinzugefuegt", reply = ToReplyDto(reply) });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    private static GlobalNotesEntryType ParseNoteType(string sourceType)
    {
        return string.Equals(sourceType, "Funk", StringComparison.OrdinalIgnoreCase)
            ? GlobalNotesEntryType.EinsatzUpdate
            : GlobalNotesEntryType.Manual;
    }

    private static NoteDto ToNoteDto(GlobalNotesEntry note)
    {
        var replies = note.Replies
            .OrderBy(r => r.Timestamp)
            .Select(ToReplyDto)
            .ToList();

        return new NoteDto(
            note.Id,
            note.Timestamp,
            note.Text,
            note.SourceTeamId,
            note.SourceTeamName,
            note.SourceType,
            note.CreatedBy,
            note.IsEdited,
            replies,
            note.FormattedTimestamp,
            note.FormattedDate);
    }

    private static ReplyDto ToReplyDto(GlobalNotesReply reply)
    {
        return new ReplyDto(
            reply.Id,
            reply.NoteId,
            reply.Timestamp,
            reply.Text,
            reply.SourceTeamId,
            reply.SourceTeamName,
            reply.CreatedBy,
            reply.FormattedTimestamp);
    }

    public sealed record CreateNoteRequest(
        string Text,
        string? SourceType,
        string? SourceTeamId,
        string? SourceTeamName,
        string? CreatedBy);

    public sealed record CreateReplyRequest(
        string Text,
        string? SourceTeamId,
        string? SourceTeamName,
        string? CreatedBy);

    public sealed record NoteDto(
        string Id,
        DateTime Timestamp,
        string Text,
        string SourceTeamId,
        string SourceTeamName,
        string SourceType,
        string CreatedBy,
        bool IsEdited,
        IReadOnlyList<ReplyDto> Replies,
        string FormattedTimestamp,
        string FormattedDate);

    public sealed record ReplyDto(
        string Id,
        string NoteId,
        DateTime Timestamp,
        string Text,
        string SourceTeamId,
        string SourceTeamName,
        string CreatedBy,
        string FormattedTimestamp);
}
