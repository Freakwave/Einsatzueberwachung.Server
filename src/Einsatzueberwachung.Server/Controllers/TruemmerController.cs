using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace Einsatzueberwachung.Server.Controllers;

[ApiController]
[Route("api/truemmer")]
public class TruemmerController : ControllerBase
{
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IEinsatzService _einsatzService;

    public TruemmerController(IEinsatzService einsatzService)
    {
        _einsatzService = einsatzService;
    }

    [HttpPost("karten")]
    [RequestSizeLimit(MaxImageBytes + 1024)]
    public async Task<IActionResult> UploadKarte([FromForm] IFormFile file, [FromForm] string? title)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Keine Datei übermittelt." });

        if (file.Length > MaxImageBytes)
            return BadRequest(new { error = $"Datei zu groß (max. {MaxImageBytes / (1024 * 1024)} MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = "Nur JPG, PNG oder WEBP erlaubt." });

        var einsatzId = ResolveEinsatzId();
        var dir = Path.Combine(AppPathResolver.GetTruemmerDirectory(), einsatzId);
        Directory.CreateDirectory(dir);

        var karteId = Guid.NewGuid();
        var fileName = karteId.ToString() + ext;
        var fullPath = Path.Combine(dir, fileName);

        await using (var fs = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
        }

        // Bildmaße via SkiaSharp lesen
        int width, height;
        try
        {
            using var stream = System.IO.File.OpenRead(fullPath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                System.IO.File.Delete(fullPath);
                return BadRequest(new { error = "Bilddatei konnte nicht gelesen werden." });
            }
            width = codec.Info.Width;
            height = codec.Info.Height;
        }
        catch
        {
            System.IO.File.Delete(fullPath);
            return BadRequest(new { error = "Bilddatei ungültig." });
        }

        var karte = new TruemmerKarte
        {
            Id = karteId,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title!,
            ImageRelativePath = Path.Combine("truemmer", einsatzId, fileName).Replace('\\', '/'),
            ImageWidthPx = width,
            ImageHeightPx = height,
            UploadedAt = DateTime.Now
        };
        await _einsatzService.AddTruemmerKarteAsync(karte);

        return Ok(karte);
    }

    [HttpGet("karten/{id:guid}/image")]
    public IActionResult GetImage(Guid id)
    {
        var karte = _einsatzService.CurrentEinsatz.TruemmerKarten?.FirstOrDefault(k => k.Id == id);
        if (karte is null) return NotFound();

        var fullPath = Path.Combine(AppPathResolver.GetDataDirectory(), karte.ImageRelativePath);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var mime = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return PhysicalFile(fullPath, mime);
    }

    [HttpDelete("karten/{id:guid}")]
    public async Task<IActionResult> DeleteKarte(Guid id)
    {
        var karte = _einsatzService.CurrentEinsatz.TruemmerKarten?.FirstOrDefault(k => k.Id == id);
        if (karte is null) return NotFound();

        var fullPath = Path.Combine(AppPathResolver.GetDataDirectory(), karte.ImageRelativePath);
        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); } catch { /* swallow */ }
        }

        await _einsatzService.RemoveTruemmerKarteAsync(id);
        return NoContent();
    }

    private string ResolveEinsatzId()
    {
        // Aktueller Einsatz hat keinen eigenen Identifier — wir nutzen die EinsatzNummer falls vorhanden,
        // sonst eine stabile Datums-Repräsentation der Alarmierungszeit.
        var einsatz = _einsatzService.CurrentEinsatz;
        if (!string.IsNullOrWhiteSpace(einsatz.EinsatzNummer))
            return SanitizeFolderName(einsatz.EinsatzNummer);
        if (einsatz.AlarmierungsZeit.HasValue)
            return einsatz.AlarmierungsZeit.Value.ToString("yyyyMMddHHmm");
        return einsatz.EinsatzDatum.ToString("yyyyMMddHHmm");
    }

    private static string SanitizeFolderName(string raw)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = raw.Where(c => !invalid.Contains(c)).ToArray();
        return new string(arr);
    }
}
