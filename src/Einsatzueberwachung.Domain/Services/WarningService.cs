using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ISettingsService _settingsService;

        // In-memory cache of rule configs so AddWarning (synchronous) never blocks on async I/O.
        // Populated lazily on first access and kept in sync on every SaveRuleConfigAsync call.
        private Dictionary<string, WarningRuleConfig>? _rulesCache;
        private readonly SemaphoreSlim _rulesCacheLock = new(1, 1);

        public event Action<WarningEntry>? WarningAdded;

        public WarningService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

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
            // Apply per-source rule: check if the source is enabled and apply any level override.
            if (!string.IsNullOrEmpty(warning.Source))
            {
                var config = GetRuleConfig(warning.Source);
                if (!config.IsEnabled)
                    return;

                if (config.LevelOverride.HasValue)
                    warning.Level = config.LevelOverride.Value;
            }

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

        public WarningRuleConfig GetRuleConfig(string source)
        {
            var cache = EnsureRulesCache();
            return cache.TryGetValue(source, out var config)
                ? config
                : new WarningRuleConfig(); // default: enabled, no level override
        }

        public async Task SaveRuleConfigAsync(string source, WarningRuleConfig config)
        {
            var settings = await _settingsService.GetAppSettingsAsync();
            settings.WarningRules[source] = config;
            await _settingsService.SaveAppSettingsAsync(settings);

            // Keep in-memory cache in sync so subsequent AddWarning calls see the new config.
            await _rulesCacheLock.WaitAsync();
            try
            {
                _rulesCache ??= new Dictionary<string, WarningRuleConfig>();
                _rulesCache[source] = config;
            }
            finally
            {
                _rulesCacheLock.Release();
            }
        }

        /// <summary>
        /// Returns the rules cache, loading from settings on the first call.
        /// Uses a double-checked locking pattern; subsequent calls are lock-free.
        /// </summary>
        private Dictionary<string, WarningRuleConfig> EnsureRulesCache()
        {
            if (_rulesCache is not null)
                return _rulesCache;

            _rulesCacheLock.Wait();
            try
            {
                if (_rulesCache is not null)
                    return _rulesCache;

                var settings = _settingsService.GetAppSettingsAsync()
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                _rulesCache = new Dictionary<string, WarningRuleConfig>(settings.WarningRules);
            }
            finally
            {
                _rulesCacheLock.Release();
            }

            return _rulesCache;
        }
    }
}
