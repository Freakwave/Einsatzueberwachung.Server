using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Einsatzueberwachung.Server.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components;

public partial class DroneStreamGrid : IAsyncDisposable
{
    [Inject] IMasterDataService MasterDataService { get; set; } = default!;
    [Inject] NavigationManager Navigation { get; set; } = default!;
    [Inject] IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IEnumerable<Team>? Teams { get; set; }
    [Parameter] public string Variant { get; set; } = "lage";

    private readonly List<DroneTeamView> _droneTeams = new();
    private Dictionary<string, DroneEntry> _droneById = new(StringComparer.OrdinalIgnoreCase);
    private string _host = "";
    private bool _hasHlsTiles;
    private ElementReference _rootRef;
    private string _lastTeamSig = "";

    protected override void OnInitialized()
    {
        var uri = new Uri(Navigation.Uri);
        _host = uri.Host;
    }

    protected override async Task OnParametersSetAsync()
    {
        var sig = ComputeTeamSignature();
        if (sig == _lastTeamSig && _droneTeams.Count > 0)
        {
            return;
        }
        _lastTeamSig = sig;
        await RefreshDroneLookupAsync();
        BuildView();
    }

    private string ComputeTeamSignature()
    {
        if (Teams is null) return string.Empty;
        return string.Join("|", Teams
            .Where(t => t.IsDroneTeam && !string.IsNullOrWhiteSpace(t.DroneId))
            .Select(t => $"{t.TeamId}:{t.DroneId}:{t.TeamName}"));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_hasHlsTiles)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("DroneStream.attachAllInContainer", _rootRef);
            }
            catch (ObjectDisposedException) { }
            catch (JSDisconnectedException) { }
        }
    }

    private async Task RefreshDroneLookupAsync()
    {
        var ids = (Teams ?? Enumerable.Empty<Team>())
            .Where(t => t.IsDroneTeam && !string.IsNullOrWhiteSpace(t.DroneId))
            .Select(t => t.DroneId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ids.Count == 0)
        {
            _droneById = new Dictionary<string, DroneEntry>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var all = await MasterDataService.GetDroneListAsync();
        _droneById = all
            .Where(d => ids.Contains(d.Id))
            .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    private void BuildView()
    {
        _droneTeams.Clear();
        if (Teams is null) return;

        foreach (var team in Teams.Where(t => t.IsDroneTeam && !string.IsNullOrWhiteSpace(t.DroneId)))
        {
            _droneById.TryGetValue(team.DroneId, out var drone);
            var url = drone?.LivestreamUrl ?? string.Empty;
            var kind = DroneStreamUrlClassifier.Classify(url);
            _droneTeams.Add(new DroneTeamView(team, drone, kind));
        }

        _hasHlsTiles = _droneTeams.Any(p => p.Kind == DroneStreamKind.Hls);
    }

    public async ValueTask DisposeAsync()
    {
        // Keine eigenen Subscriptions; HLS-Instanzen leben am DOM-Element und werden
        // beim Aufraeumen des DOM ueber browser-natives Garbage-Collection freigegeben.
        // Falls die Komponente in einer langlebigen Page rotiert wird, koennte hier
        // DroneStream.detach pro Video-Element aufgerufen werden.
        await Task.CompletedTask;
    }

    private sealed record DroneTeamView(Team Team, DroneEntry? Drone, DroneStreamKind Kind);
}
