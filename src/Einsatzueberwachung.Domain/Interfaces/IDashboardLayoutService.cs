using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public interface IDashboardLayoutService
    {
        Task<List<DashboardPanelConfig>> LoadLayoutAsync(string fuehrungsassistentName);
        Task SaveLayoutAsync(string fuehrungsassistentName, List<DashboardPanelConfig> panels);
        List<DashboardPanelConfig> GetDefaultLayout();
    }
}
