using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        /// <remarks>
        /// If the warning's <see cref="WarningEntry.Source"/> is configured as disabled the call
        /// is silently ignored. If a level-override is configured the entry's level is adjusted
        /// before it is stored.
        /// </remarks>
        void AddWarning(WarningEntry warning);

        /// <summary>Removes a specific warning by ID (e.g. after the issue is resolved).</summary>
        void DismissWarning(string id);

        /// <summary>Removes all stored warnings.</summary>
        void ClearAll();

        /// <summary>
        /// Returns the persisted <see cref="WarningRuleConfig"/> for <paramref name="source"/>,
        /// or a default (enabled, no level-override) if none has been saved yet.
        /// </summary>
        WarningRuleConfig GetRuleConfig(string source);

        /// <summary>Persists the <see cref="WarningRuleConfig"/> for <paramref name="source"/>.</summary>
        Task SaveRuleConfigAsync(string source, WarningRuleConfig config);
    }
}
