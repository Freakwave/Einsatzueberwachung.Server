using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class EinsatzService
    {
        public Task AddTruemmerKarteAsync(TruemmerKarte karte)
        {
            _currentEinsatz.TruemmerKarten ??= new List<TruemmerKarte>();
            _currentEinsatz.TruemmerKarten.Add(karte);
            TruemmerKarteAdded?.Invoke(karte);
            EinsatzChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task RemoveTruemmerKarteAsync(Guid id)
        {
            var karte = _currentEinsatz.TruemmerKarten?.FirstOrDefault(k => k.Id == id);
            if (karte is null) return Task.CompletedTask;

            _currentEinsatz.TruemmerKarten!.Remove(karte);

            // Zugehörige Areas mit entfernen — verwaiste Polygone wären sinnlos.
            if (_currentEinsatz.TruemmerAreas is not null)
            {
                var orphans = _currentEinsatz.TruemmerAreas.Where(a => a.TruemmerKarteId == id).ToList();
                foreach (var orphan in orphans)
                {
                    _currentEinsatz.TruemmerAreas.Remove(orphan);
                    TruemmerAreaRemoved?.Invoke(orphan.Id);
                }
            }

            TruemmerKarteRemoved?.Invoke(id);
            EinsatzChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task UpsertTruemmerAreaAsync(TruemmerArea area)
        {
            if (area.Id == Guid.Empty) area.Id = Guid.NewGuid();
            _currentEinsatz.TruemmerAreas ??= new List<TruemmerArea>();

            var idx = _currentEinsatz.TruemmerAreas.FindIndex(a => a.Id == area.Id);
            if (idx < 0)
                _currentEinsatz.TruemmerAreas.Add(area);
            else
                _currentEinsatz.TruemmerAreas[idx] = area;

            TruemmerAreaUpserted?.Invoke(area);
            EinsatzChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task RemoveTruemmerAreaAsync(Guid id)
        {
            var area = _currentEinsatz.TruemmerAreas?.FirstOrDefault(a => a.Id == id);
            if (area is null) return Task.CompletedTask;

            _currentEinsatz.TruemmerAreas!.Remove(area);
            TruemmerAreaRemoved?.Invoke(id);
            EinsatzChanged?.Invoke();
            return Task.CompletedTask;
        }
    }
}
