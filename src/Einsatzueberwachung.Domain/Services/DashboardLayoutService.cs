using System.Text.Json;
using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Services
{
    public class DashboardLayoutService : IDashboardLayoutService
    {
        private readonly string _layoutsDir;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

        public DashboardLayoutService()
        {
            _layoutsDir = Path.Combine(AppPathResolver.GetDataDirectory(), "dashboard-layouts");
            Directory.CreateDirectory(_layoutsDir);
        }

        public async Task<List<DashboardPanelConfig>> LoadLayoutAsync(string fuehrungsassistentName)
        {
            if (string.IsNullOrWhiteSpace(fuehrungsassistentName))
                return GetDefaultLayout();

            var path = BuildPath(fuehrungsassistentName);
            if (!File.Exists(path))
                return GetDefaultLayout();

            try
            {
                var json = await File.ReadAllTextAsync(path);
                var layout = JsonSerializer.Deserialize<List<DashboardPanelConfig>>(json);
                return ValidateLayout(layout);
            }
            catch
            {
                return GetDefaultLayout();
            }
        }

        public async Task SaveLayoutAsync(string fuehrungsassistentName, List<DashboardPanelConfig> panels)
        {
            var key = string.IsNullOrWhiteSpace(fuehrungsassistentName) ? "_default" : fuehrungsassistentName;
            var path = BuildPath(key);

            await _lock.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(panels, _jsonOpts));
            }
            finally
            {
                _lock.Release();
            }
        }

        public List<DashboardPanelConfig> GetDefaultLayout() =>
        [
            new() { PanelId = KnownPanels.EinsatzInfo,  IsVisible = true },
            new() { PanelId = KnownPanels.Vermissten,   IsVisible = true },
            new() { PanelId = KnownPanels.Wetter,       IsVisible = true },
            new() { PanelId = KnownPanels.Teams,        IsVisible = true },
            new() { PanelId = KnownPanels.Suchgebiete,  IsVisible = true },
            new() { PanelId = KnownPanels.Notizen,      IsVisible = true },
        ];

        // Stellt sicher dass alle bekannten Panels vorhanden sind; entfernt veraltete (z. B. minimap)
        private List<DashboardPanelConfig> ValidateLayout(List<DashboardPanelConfig>? saved)
        {
            if (saved is null || saved.Count == 0)
                return GetDefaultLayout();

            var knownIds = KnownPanels.Labels.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = saved.Where(p => knownIds.Contains(p.PanelId)).ToList();

            var defaults = GetDefaultLayout();
            foreach (var def in defaults)
            {
                if (!result.Any(p => p.PanelId == def.PanelId))
                    result.Add(def);
            }

            // Sicherstellen dass die Reihenfolge der FixedOrder entspricht
            return [.. KnownPanels.FixedOrder
                .Select(id => result.FirstOrDefault(p => p.PanelId == id))
                .Where(p => p is not null)!];
        }

        private string BuildPath(string key)
        {
            var safe = string.Concat(key.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
            return Path.Combine(_layoutsDir, $"layout_{safe}.json");
        }
    }
}
