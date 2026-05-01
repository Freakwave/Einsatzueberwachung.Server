using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Server.Services;

internal static class TeamMobileLookup
{
    /// <summary>
    /// Sucht das Suchgebiet zu einem Team. Beruecksichtigt beide Zuordnungs-Richtungen
    /// (Team.SearchAreaId und SearchArea.AssignedTeamId) und stellt sicher, dass
    /// Coordinates gefuellt sind (Fallback aus GeoJsonData).
    /// </summary>
    public static SearchArea? FindSearchAreaForTeam(EinsatzData? einsatz, Team team)
    {
        if (einsatz?.SearchAreas == null || einsatz.SearchAreas.Count == 0) return null;

        SearchArea? area = null;
        if (!string.IsNullOrWhiteSpace(team.SearchAreaId))
            area = einsatz.SearchAreas.FirstOrDefault(a => a.Id == team.SearchAreaId);
        if (area == null)
            area = einsatz.SearchAreas.FirstOrDefault(a => a.AssignedTeamId == team.TeamId);

        if (area != null && (area.Coordinates == null || area.Coordinates.Count == 0)
            && !string.IsNullOrWhiteSpace(area.GeoJsonData))
        {
            ExtractCoordinatesFromGeoJson(area);
        }

        return area;
    }

    private static void ExtractCoordinatesFromGeoJson(SearchArea area)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(area.GeoJsonData);
            if (doc.RootElement.TryGetProperty("geometry", out var geometry) &&
                geometry.TryGetProperty("coordinates", out var coordinates))
            {
                area.Coordinates = new List<(double, double)>();
                var firstRing = coordinates[0];
                foreach (var coord in firstRing.EnumerateArray())
                    area.Coordinates.Add((coord[1].GetDouble(), coord[0].GetDouble()));
            }
        }
        catch
        {
            // GeoJSON konnte nicht geparst werden – Koordinaten bleiben leer
        }
    }
}
