using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzService
    {
        public event Action<TeamTrackSnapshot>? TrackSnapshotAdded;

        public Task AddTrackSnapshotAsync(TeamTrackSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            _currentEinsatz.TrackSnapshots ??= new List<TeamTrackSnapshot>();
            _currentEinsatz.TrackSnapshots.Add(snapshot);

            TrackSnapshotAdded?.Invoke(snapshot);

            return Task.CompletedTask;
        }
    }
}
