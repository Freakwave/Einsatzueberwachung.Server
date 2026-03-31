using Einsatzueberwachung.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Einsatzueberwachung.Mobile.Services;

public sealed class MobileSignalRClient : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<MobileSignalRClient> _logger;
    private HubConnection? _connection;

    public event Action<string, string>? UpdateReceived;

    public MobileSignalRClient(NavigationManager navigationManager, ILogger<MobileSignalRClient> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public async Task EnsureConnectedAsync()
    {
        if (_connection is { State: HubConnectionState.Connected })
        {
            return;
        }

        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("/hubs/einsatz"))
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string>("einsatz:update", (eventName, payload) =>
            {
                UpdateReceived?.Invoke(eventName, payload);
            });

            _connection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "SignalR reconnecting");
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                _logger.LogInformation("SignalR reconnected with id {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };
        }

        if (_connection.State is HubConnectionState.Disconnected)
        {
            await _connection.StartAsync();
        }
    }

    public async Task<EinsatzData> GetCurrentEinsatzAsync()
    {
        await EnsureConnectedAsync();
        return await _connection!.InvokeAsync<EinsatzData>("GetCurrentEinsatz");
    }

    public async Task<IReadOnlyList<Team>> GetTeamsAsync()
    {
        await EnsureConnectedAsync();
        var teams = await _connection!.InvokeAsync<List<Team>>("GetTeamsSnapshot");
        return teams;
    }

    public async Task<IReadOnlyList<GlobalNotesEntry>> GetNotesAsync(string filter)
    {
        await EnsureConnectedAsync();
        var notes = await _connection!.InvokeAsync<List<GlobalNotesEntry>>("GetNotesSnapshot", filter);
        return notes;
    }

    public async Task StartEinsatzAsync(EinsatzData einsatzData, string? initialNote)
    {
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("StartEinsatzFromMobile", einsatzData, initialNote);
    }

    public async Task AddNoteAsync(string text, string sourceType)
    {
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("AddGlobalNoteFromMobile", text, sourceType);
    }

    public async Task AddReplyAsync(string noteId, string text)
    {
        await EnsureConnectedAsync();
        await _connection!.InvokeAsync("AddReplyFromMobile", noteId, text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
