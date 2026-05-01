using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzService
    {
        public Task UpdateTeamPhoneLocationAsync(string teamId, double lat, double lng, double? accuracy = null)
        {
            var team = _teams.FirstOrDefault(t => t.TeamId == teamId);
            if (team == null) return Task.CompletedTask;

            var loc = new TeamPhoneLocation
            {
                Latitude = lat,
                Longitude = lng,
                Timestamp = GetServerNowLocal(),
                Accuracy = accuracy
            };
            _phoneLocations[teamId] = loc;
            TeamPhoneLocationChanged?.Invoke(teamId, team.TeamName, loc);
            return Task.CompletedTask;
        }
    }
}
