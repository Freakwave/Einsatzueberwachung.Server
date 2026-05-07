using System.Text.RegularExpressions;

namespace Einsatzueberwachung.Server.Services;

public enum DroneStreamKind
{
    None,
    YouTube,
    Twitch,
    Hls,
    Mjpeg,
    Generic
}

public static class DroneStreamUrlClassifier
{
    private static readonly Regex YouTubeWatch = new(
        @"(?:youtube\.com/watch\?(?:[^&]*&)*v=|youtu\.be/|youtube\.com/embed/|youtube\.com/live/)([A-Za-z0-9_-]{6,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TwitchChannel = new(
        @"twitch\.tv/([A-Za-z0-9_]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static DroneStreamKind Classify(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return DroneStreamKind.None;
        var u = url.Trim();

        if (YouTubeWatch.IsMatch(u)) return DroneStreamKind.YouTube;
        if (TwitchChannel.IsMatch(u)) return DroneStreamKind.Twitch;

        // Pfad ohne Querystring fuer Endungs-Check
        var pathPart = u.Split('?', 2)[0];
        if (pathPart.EndsWith(".m3u8", System.StringComparison.OrdinalIgnoreCase)) return DroneStreamKind.Hls;
        if (pathPart.EndsWith(".mjpg", System.StringComparison.OrdinalIgnoreCase)
            || pathPart.EndsWith(".mjpeg", System.StringComparison.OrdinalIgnoreCase))
            return DroneStreamKind.Mjpeg;

        return DroneStreamKind.Generic;
    }

    /// <summary>
    /// Wandelt eine YouTube-URL (watch / youtu.be / live / embed) in eine Embed-URL um.
    /// Liefert null wenn keine Video-ID extrahierbar ist.
    /// </summary>
    public static string? ToYouTubeEmbed(string url, bool autoplay = true, bool mute = true, bool controls = false)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var match = YouTubeWatch.Match(url);
        if (!match.Success) return null;
        var id = match.Groups[1].Value;
        var query = $"autoplay={(autoplay ? 1 : 0)}&mute={(mute ? 1 : 0)}&controls={(controls ? 1 : 0)}&playsinline=1";
        return $"https://www.youtube.com/embed/{id}?{query}";
    }

    /// <summary>
    /// Wandelt eine Twitch-Kanal-URL in eine Embed-URL um. parentHost ist die Domain, unter der der iframe laeuft.
    /// </summary>
    public static string? ToTwitchEmbed(string url, string parentHost, bool muted = true)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(parentHost)) return null;
        var match = TwitchChannel.Match(url);
        if (!match.Success) return null;
        var channel = match.Groups[1].Value;
        return $"https://player.twitch.tv/?channel={channel}&parent={parentHost}&muted={(muted ? "true" : "false")}&autoplay=true";
    }
}
