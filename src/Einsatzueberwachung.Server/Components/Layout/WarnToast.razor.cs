using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Microsoft.AspNetCore.Components;

namespace Einsatzueberwachung.Server.Components.Layout;

public partial class WarnToast : IAsyncDisposable
{
    [Inject] private IWarningService WarningService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly Queue<WarningEntry> _queue = new();
    private WarningEntry? _current;
    private bool _visible;
    private CancellationTokenSource? _autoDismissCts;

    protected override void OnInitialized()
    {
        WarningService.WarningAdded += OnWarningAdded;
    }

    private void OnWarningAdded(WarningEntry warning)
    {
        _ = InvokeAsync(async () =>
        {
            _queue.Enqueue(warning);
            if (_current is null)
            {
                await ShowNextAsync();
            }
        });
    }

    private async Task ShowNextAsync()
    {
        _autoDismissCts?.Cancel();
        _autoDismissCts?.Dispose();
        _autoDismissCts = null;

        if (!_queue.TryDequeue(out var next))
        {
            _current = null;
            _visible = false;
            StateHasChanged();
            return;
        }

        _visible = false;
        _current = next;
        StateHasChanged();

        // Brief delay to allow the DOM to render before triggering the entrance animation
        await Task.Delay(50);
        _visible = true;
        StateHasChanged();

        _autoDismissCts = new CancellationTokenSource();
        _ = AutoDismissAsync(_autoDismissCts.Token);
    }

    private async Task AutoDismissAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(5000, ct);
            await InvokeAsync(async () =>
            {
                _visible = false;
                StateHasChanged();
                // Let the exit animation play before removing the element
                await Task.Delay(300);
                await ShowNextAsync();
            });
        }
        catch (OperationCanceledException)
        {
            // Dismissed manually or replaced
        }
    }

    private async Task DismissAsync()
    {
        _autoDismissCts?.Cancel();
        _visible = false;
        StateHasChanged();
        await Task.Delay(300);
        await ShowNextAsync();
    }

    private async Task OnToastClickedAsync()
    {
        if (_current?.NavigationUrl is { } url)
        {
            await DismissAsync();
            Navigation.NavigateTo(url);
        }
        else
        {
            await DismissAsync();
        }
    }

    private static string GetIcon(WarningLevel level) => level switch
    {
        WarningLevel.Critical => "bi-exclamation-octagon-fill",
        WarningLevel.Info => "bi-info-circle-fill",
        _ => "bi-exclamation-triangle-fill"
    };

    public async ValueTask DisposeAsync()
    {
        WarningService.WarningAdded -= OnWarningAdded;
        _autoDismissCts?.Cancel();
        _autoDismissCts?.Dispose();
        await Task.CompletedTask;
    }
}
