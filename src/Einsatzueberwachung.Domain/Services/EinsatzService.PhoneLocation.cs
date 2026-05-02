using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzService
    {
        private readonly object _phoneLocationsLock = new();

        public Task UpdateTeamPhoneLocationAsync(string teamId, double lat, double lng, double? accuracy = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return Task.CompletedTask;

            if (double.IsNaN(lat) || double.IsInfinity(lat) || lat < -90 || lat > 90)
                return Task.CompletedTask;
            if (double.IsNaN(lng) || double.IsInfinity(lng) || lng < -180 || lng > 180)
                return Task.CompletedTask;
            if (accuracy.HasValue && (double.IsNaN(accuracy.Value) || double.IsInfinity(accuracy.Value)))
                accuracy = null;

            Team? team;
            lock (_teams)
            {
                team = _teams.FirstOrDefault(t => t.TeamId == teamId);
            }
            if (team == null) return Task.CompletedTask;

            var loc = new TeamPhoneLocation
            {
                Latitude = lat,
                Longitude = lng,
                Timestamp = GetServerNowLocal(),
                Accuracy = accuracy
            };

            lock (_phoneLocationsLock)
            {
                _phoneLocations[teamId] = loc;
            }

            try
            {
                TeamPhoneLocationChanged?.Invoke(teamId, team.TeamName, loc);
            }
            catch
            {
                // Subscriber-Fehler dürfen den Request-Pfad nicht abbrechen
            }
            return Task.CompletedTask;
        }
    }
}
