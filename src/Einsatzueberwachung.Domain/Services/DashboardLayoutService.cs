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
            new() { PanelId = KnownPanels.EinsatzInfo,  ColSpan = 4,  Order = 0, IsVisible = true  },
            new() { PanelId = KnownPanels.Wetter,       ColSpan = 4,  Order = 1, IsVisible = true  },
            new() { PanelId = KnownPanels.Notizen,      ColSpan = 4,  Order = 2, IsVisible = true  },
            new() { PanelId = KnownPanels.Teams,        ColSpan = 12, Order = 3, IsVisible = true  },
            new() { PanelId = KnownPanels.Suchgebiete,  ColSpan = 6,  Order = 4, IsVisible = false },
            new() { PanelId = KnownPanels.Minimap,      ColSpan = 6,  Order = 5, IsVisible = false, PanelHeight = 350 },
            new() { PanelId = KnownPanels.Vermissten,   ColSpan = 6,  Order = 6, IsVisible = false },
        ];

        // Stellt sicher dass alle bekannten Panels vorhanden sind (neue Panels nach Update)
        private List<DashboardPanelConfig> ValidateLayout(List<DashboardPanelConfig>? saved)
        {
            if (saved is null || saved.Count == 0)
                return GetDefaultLayout();

            var defaults = GetDefaultLayout();
            var result = saved.ToList();

            foreach (var def in defaults)
            {
                if (!result.Any(p => p.PanelId == def.PanelId))
                {
                    def.Order = result.Count;
                    result.Add(def);
                }
            }

            return [.. result.OrderBy(p => p.Order)];
        }

        private string BuildPath(string key)
        {
            var safe = string.Concat(key.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
            return Path.Combine(_layoutsDir, $"layout_{safe}.json");
        }
    }
}
