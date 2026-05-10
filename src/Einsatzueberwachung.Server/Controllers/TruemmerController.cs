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

        // Inhalt zuerst komplett in den Speicher lesen und Magic-Bytes + Codec validieren,
        // BEVOR wir auf Disk schreiben — keine getarnten Dateien sollen je auf Platte landen.
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        var bytes = memory.ToArray();

        if (!HasValidImageMagicBytes(bytes, ext))
            return BadRequest(new { error = "Bildinhalt passt nicht zur Dateiendung." });

        int width, height;
        try
        {
            memory.Position = 0;
            using var codec = SKCodec.Create(memory);
            if (codec is null)
                return BadRequest(new { error = "Bilddatei konnte nicht gelesen werden." });
            width = codec.Info.Width;
            height = codec.Info.Height;
        }
        catch
        {
            return BadRequest(new { error = "Bilddatei ungültig." });
        }

        var einsatzId = ResolveEinsatzId();
        var dir = Path.Combine(AppPathResolver.GetTruemmerDirectory(), einsatzId);
        Directory.CreateDirectory(dir);

        var karteId = Guid.NewGuid();
        var fileName = karteId.ToString() + ext;
        var fullPath = Path.Combine(dir, fileName);

        await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

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

    private static bool HasValidImageMagicBytes(byte[] bytes, string extension)
    {
        if (bytes.Length < 12) return false;

        // JPEG: FF D8 FF
        bool isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                  && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        // WEBP: 'RIFF' .... 'WEBP'
        bool isWebp = bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                   && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

        return extension switch
        {
            ".jpg" or ".jpeg" => isJpeg,
            ".png" => isPng,
            ".webp" => isWebp,
            _ => false
        };
    }
}
