using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services;

/// <summary>
/// Liest GPX 1.0/1.1-Dateien und extrahiert GPS-Trackpunkte als <see cref="TrackPoint"/>-Liste.
/// </summary>
public static class GpxParser
{
    private const string Gpx10Ns = "http://www.topografix.com/GPX/1/0";
    private const string Gpx11Ns = "http://www.topografix.com/GPX/1/1";

    /// <summary>
    /// Parst einen GPX-Dateistream und gibt alle Trackpunkte chronologisch sortiert zurück.
    /// Liest <c>&lt;trkpt&gt;</c> aus <c>&lt;trk&gt;/&lt;trkseg&gt;</c>; fällt auf <c>&lt;wpt&gt;</c> zurück
    /// wenn kein <c>&lt;trk&gt;</c> vorhanden ist.
    /// </summary>
    /// <exception cref="FormatException">Wenn die GPX-Datei nicht valide XML ist oder keine GPX-Struktur aufweist.</exception>
    public static async Task<List<TrackPoint>> ParseAsync(Stream gpxStream)
    {
        using var reader = new StreamReader(gpxStream);
        var content = await reader.ReadToEndAsync();
        return Parse(content);
    }

    /// <summary>
    /// Parst GPX-XML-Inhalt als String und gibt Trackpunkte zurück.
    /// </summary>
    /// <exception cref="FormatException">Wenn die GPX-Datei nicht valide ist.</exception>
    public static List<TrackPoint> Parse(string gpxContent)
    {
        if (string.IsNullOrWhiteSpace(gpxContent))
            throw new FormatException("GPX-Datei ist leer.");

        XmlDocument doc;
        try
        {
            doc = new XmlDocument();
            doc.LoadXml(gpxContent);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"GPX-Datei enthält ungültiges XML: {ex.Message}", ex);
        }

        var root = doc.DocumentElement;
        if (root == null || root.LocalName != "gpx")
            throw new FormatException("Datei ist keine gültige GPX-Datei (Root-Element ist nicht <gpx>).");

        var ns = root.NamespaceURI switch
        {
            Gpx10Ns => Gpx10Ns,
            Gpx11Ns => Gpx11Ns,
            _ => root.NamespaceURI  // Unbekannter oder leerer Namespace: trotzdem versuchen
        };

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("g", string.IsNullOrEmpty(ns) ? string.Empty : ns);

        // Versuche zuerst Trackpunkte (<trkpt>)
        var points = ExtractTrackPoints(doc, nsMgr, ns);

        // Fallback 1: Wegpunkte (<wpt>) wenn kein Track vorhanden
        if (points.Count == 0)
            points = ExtractWaypoints(doc, nsMgr, ns);

        // Fallback 2: Routenpunkte (<rtept>) – z.B. Google Maps, OsmAnd, ältere Garmin-Exporte
        if (points.Count == 0)
            points = ExtractRoutePoints(doc, nsMgr, ns);

        if (points.Count == 0)
            throw new FormatException("GPX-Datei enthält keine Trackpunkte (<trkpt>), Wegpunkte (<wpt>) oder Routenpunkte (<rtept>).");

        // Chronologisch sortieren (Timestamps können ungeordnet vorliegen).
        // Nur sortieren wenn echte Zeitstempel vorhanden sind – List<T>.Sort ist instabil,
        // würde bei reinen DateTime.MinValue-Punkten die Datei-Reihenfolge zerstören.
        if (points.Any(p => p.Timestamp != DateTime.MinValue))
            points.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        return points;
    }

    private static List<TrackPoint> ExtractTrackPoints(XmlDocument doc, XmlNamespaceManager nsMgr, string ns)
    {
        var points = new List<TrackPoint>();

        var trkptNodes = string.IsNullOrEmpty(ns)
            ? doc.SelectNodes("//trkpt")
            : doc.SelectNodes("//g:trkpt", nsMgr);

        if (trkptNodes == null) return points;

        foreach (XmlNode node in trkptNodes)
        {
            if (TryParsePoint(node, nsMgr, ns, out var point))
                points.Add(point);
        }

        return points;
    }

    private static List<TrackPoint> ExtractWaypoints(XmlDocument doc, XmlNamespaceManager nsMgr, string ns)
    {
        var points = new List<TrackPoint>();

        var wptNodes = string.IsNullOrEmpty(ns)
            ? doc.SelectNodes("//wpt")
            : doc.SelectNodes("//g:wpt", nsMgr);

        if (wptNodes == null) return points;

        foreach (XmlNode node in wptNodes)
        {
            if (TryParsePoint(node, nsMgr, ns, out var point))
                points.Add(point);
        }

        return points;
    }

    private static List<TrackPoint> ExtractRoutePoints(XmlDocument doc, XmlNamespaceManager nsMgr, string ns)
    {
        var points = new List<TrackPoint>();

        var rteptNodes = string.IsNullOrEmpty(ns)
            ? doc.SelectNodes("//rtept")
            : doc.SelectNodes("//g:rtept", nsMgr);

        if (rteptNodes == null) return points;

        foreach (XmlNode node in rteptNodes)
        {
            if (TryParsePoint(node, nsMgr, ns, out var point))
                points.Add(point);
        }

        return points;
    }

    private static bool TryParsePoint(XmlNode node, XmlNamespaceManager nsMgr, string ns, out TrackPoint point)
    {
        point = new TrackPoint();

        var latAttr = node.Attributes?["lat"]?.Value;
        var lonAttr = node.Attributes?["lon"]?.Value;

        if (!double.TryParse(latAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(lonAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return false;

        // Koordinaten-Validierung
        if (lat < -90.0 || lat > 90.0 || lon < -180.0 || lon > 180.0)
            return false;

        point.Latitude = lat;
        point.Longitude = lon;

        // Zeitstempel aus <time>-Kind-Element
        var timeNode = string.IsNullOrEmpty(ns)
            ? node.SelectSingleNode("time")
            : node.SelectSingleNode("g:time", nsMgr);

        if (timeNode?.InnerText is { Length: > 0 } timeText &&
            DateTime.TryParse(timeText, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ts))
        {
            point.Timestamp = ts;
        }
        else
        {
            // Kein Zeitstempel: Punkt behalten, aber ohne Zeit → wird nach Reihenfolge sortiert
            point.Timestamp = DateTime.MinValue;
        }

        return true;
    }

    /// <summary>
    /// Gibt die Anzahl der Punkte zurück, die durch das angegebene Zeitfenster ausgeschnitten würden.
    /// Nützlich für die UI-Vorschau.
    /// </summary>
    public static (int Included, int Excluded) CountWithTimeFilter(
        IReadOnlyList<TrackPoint> points, DateTime? startTime, DateTime? endTime)
    {
        var included = points.Count(p =>
            (startTime == null || p.Timestamp >= startTime.Value) &&
            (endTime == null || p.Timestamp <= endTime.Value));

        return (included, points.Count - included);
    }

    /// <summary>
    /// Filtert eine Punktliste auf das angegebene Zeitfenster.
    /// Punkte mit <see cref="DateTime.MinValue"/> als Timestamp werden immer eingeschlossen.
    /// </summary>
    public static List<TrackPoint> ApplyTimeFilter(
        IReadOnlyList<TrackPoint> points, DateTime? startTime, DateTime? endTime)
    {
        return points.Where(p =>
            p.Timestamp == DateTime.MinValue ||
            ((startTime == null || p.Timestamp >= startTime.Value) &&
             (endTime == null || p.Timestamp <= endTime.Value))
        ).ToList();
    }
}
