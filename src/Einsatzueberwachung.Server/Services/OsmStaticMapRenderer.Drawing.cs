using SkiaSharp;

namespace Einsatzueberwachung.Server.Services;

public sealed partial class OsmStaticMapRenderer
{
    private static void DrawMarker(SKCanvas canvas, SKPoint center, SKColor color, string label)
    {
        using var borderPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White, IsAntialias = true };
        canvas.DrawCircle(center, 10, borderPaint);

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = true };
        canvas.DrawCircle(center, 8, fillPaint);

        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
        using var bgPaint = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
        labelFont.MeasureText(label, out var lblBounds);
        canvas.DrawRoundRect(center.X + 13, center.Y - lblBounds.Height / 2 - 2, lblBounds.Width + 6, lblBounds.Height + 4, 3, 3, bgPaint);

        using var textPaint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(label, center.X + 16, center.Y + lblBounds.Height / 2 - 1, SKTextAlign.Left, labelFont, textPaint);
    }

    private static void DrawAreaLabel(SKCanvas canvas, SKPoint center, string text, SKColor areaColor)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var font = new SKFont(typeface, 16);
        font.MeasureText(text, out var bounds);

        const float pad = 5f;
        var bgRect = new SKRoundRect(
            new SKRect(
                center.X - bounds.Width / 2f - pad,
                center.Y - bounds.Height / 2f - pad,
                center.X + bounds.Width / 2f + pad,
                center.Y + bounds.Height / 2f + pad),
            4f, 4f);

        using var bgPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(255, 255, 255, 215), IsAntialias = true };
        canvas.DrawRoundRect(bgRect, bgPaint);

        using var borderPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = areaColor, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawRoundRect(bgRect, borderPaint);

        using var textPaint = new SKPaint { Color = new SKColor(30, 30, 30), IsAntialias = true };
        canvas.DrawText(text, center.X, center.Y + bounds.Height / 2f - 2f, SKTextAlign.Center, font, textPaint);
    }

    private static void DrawNorthArrow(SKCanvas canvas, int width, int height)
    {
        var cx = width - 38f;
        var cy = 42f;
        const float r = 26f;

        using var bgPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
        using var borderPaint = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(120, 120, 120), StrokeWidth = 1f, IsAntialias = true };
        canvas.DrawCircle(cx, cy, r, bgPaint);
        canvas.DrawCircle(cx, cy, r, borderPaint);

        using var arrowPath = new SKPath();
        arrowPath.MoveTo(cx, cy - r + 7f);
        arrowPath.LineTo(cx - 7f, cy + 6f);
        arrowPath.LineTo(cx + 7f, cy + 6f);
        arrowPath.Close();
        using var arrowPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(50, 80, 200), IsAntialias = true };
        canvas.DrawPath(arrowPath, arrowPaint);

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var nFont = new SKFont(typeface, 12f);
        using var nPaint = new SKPaint { Color = new SKColor(50, 80, 200), IsAntialias = true };
        canvas.DrawText("N", cx, cy - r + 6f, SKTextAlign.Center, nFont, nPaint);
    }

    private static void DrawAttribution(SKCanvas canvas, int width, int height, string attribution)
    {
        using var font = new SKFont(SKTypeface.Default, 10);
        using var paint = new SKPaint { Color = new SKColor(80, 80, 80), IsAntialias = true };
        using var bgPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(255, 255, 255, 180) };
        font.MeasureText(attribution, out var textBounds);
        var attrX = width - textBounds.Width - 6;
        var attrY = height - 6;
        canvas.DrawRect(attrX - 3, attrY - textBounds.Height - 2, textBounds.Width + 6, textBounds.Height + 4, bgPaint);
        canvas.DrawText(attribution, attrX, attrY, SKTextAlign.Left, font, paint);
    }

    private static SKColor ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length < 7)
            return new SKColor(255, 68, 68);

        try
        {
            hex = hex.TrimStart('#');
            return new SKColor(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        catch
        {
            return new SKColor(255, 68, 68);
        }
    }

    private static double LonToTileXFloat(double lon, int zoom)
        => (lon + 180.0) / 360.0 * (1 << zoom);

    private static double LatToTileYFloat(double lat, int zoom)
    {
        var latRad = lat * Math.PI / 180.0;
        return (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * (1 << zoom);
    }

    private static int CalculateZoom(double minLat, double maxLat, double minLon, double maxLon, int imgWidth, int imgHeight)
    {
        for (int z = 17; z >= 2; z--)
        {
            var x1 = LonToTileXFloat(minLon, z) * TileSize;
            var x2 = LonToTileXFloat(maxLon, z) * TileSize;
            var y1 = LatToTileYFloat(maxLat, z) * TileSize;
            var y2 = LatToTileYFloat(minLat, z) * TileSize;

            if ((x2 - x1) <= imgWidth * 1.2 && (y2 - y1) <= imgHeight * 1.2)
                return z;
        }
        return 2;
    }
}
