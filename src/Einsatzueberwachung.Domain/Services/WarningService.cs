using System;
using System.Collections.Generic;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    /// <summary>
    /// Singleton in-memory implementation of <see cref="IWarningService"/>.
    /// Thread-safe; stores up to <see cref="MaxWarnings"/> entries (newest first).
    /// </summary>
    public sealed class WarningService : IWarningService
    {
        private const int MaxWarnings = 200;
        private readonly List<WarningEntry> _warnings = new();
        private readonly object _lock = new();

        public event Action<WarningEntry>? WarningAdded;

        public IReadOnlyList<WarningEntry> Warnings
        {
            get
            {
                lock (_lock)
                {
                    return _warnings.AsReadOnly();
                }
            }
        }

        public void AddWarning(WarningEntry warning)
        {
            lock (_lock)
            {
                _warnings.Insert(0, warning);
                if (_warnings.Count > MaxWarnings)
                    _warnings.RemoveRange(MaxWarnings, _warnings.Count - MaxWarnings);
            }

            WarningAdded?.Invoke(warning);
        }

        public void DismissWarning(string id)
        {
            lock (_lock)
            {
                _warnings.RemoveAll(w => w.Id == id);
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _warnings.Clear();
            }
        }
    }
}
