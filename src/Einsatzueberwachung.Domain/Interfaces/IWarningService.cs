using System;
using System.Collections.Generic;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    /// <summary>
    /// Central warning service that collects situational warnings from all parts of the application.
    /// Subscribe to <see cref="WarningAdded"/> to react to new warnings in real time.
    /// </summary>
    public interface IWarningService
    {
        /// <summary>Fired when a new warning is added. May be called from any thread.</summary>
        event Action<WarningEntry>? WarningAdded;

        /// <summary>All warnings, newest first (max 200 entries).</summary>
        IReadOnlyList<WarningEntry> Warnings { get; }

        /// <summary>Adds a new warning and fires <see cref="WarningAdded"/>.</summary>
        void AddWarning(WarningEntry warning);

        /// <summary>Removes a specific warning by ID (e.g. after the issue is resolved).</summary>
        void DismissWarning(string id);

        /// <summary>Removes all stored warnings.</summary>
        void ClearAll();
    }
}
