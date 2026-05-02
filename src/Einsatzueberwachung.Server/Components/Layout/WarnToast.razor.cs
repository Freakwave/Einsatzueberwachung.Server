using Einsatzueberwachung.Domain.Interfaces;
using Einsatzueberwachung.Domain.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components.Layout;

public partial class WarnToast : IAsyncDisposable
{
    [Inject] private IWarningService WarningService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

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
        if (_current?.NavigationUrl is not { } url)
        {
            await DismissAsync();
            return;
        }

        await DismissAsync();

        // Separate path from fragment so Blazor navigation works correctly
        var hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            var path = url[..hashIndex];
            var elementId = url[(hashIndex + 1)..];
            Navigation.NavigateTo(path);
            // Wait for the page to render before scrolling to the element
            await ScrollToElementAsync(elementId);
        }
        else
        {
            var currentUri = Navigation.Uri;
            var targetUri = Navigation.ToAbsoluteUri(url).ToString();
            if (string.Equals(currentUri, targetUri, StringComparison.OrdinalIgnoreCase))
            {
                var separator = url.Contains('?') ? "&" : "?";
                url = $"{url}{separator}focusNonce={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            }

            Navigation.NavigateTo(url);
        }
    }

    private async Task ScrollToElementAsync(string elementId)
    {
        // Retry a few times to handle the page still rendering after navigation
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(150 + attempt * 100);
            try
            {
                var found = await JS.InvokeAsync<bool>("warnToastScrollTo", elementId);
                if (found) return;
            }
            catch (JSDisconnectedException)
            {
                return;
            }
            catch
            {
                // JS not yet available; retry
            }
        }
    }

    private static string GetIcon(WarningLevel level) => level switch
    {
        WarningLevel.Critical => "bi-exclamation-octagon-fill",
        WarningLevel.Info => "bi-info-circle-fill",
        _ => "bi-exclamation-triangle-fill"
    };

    public ValueTask DisposeAsync()
    {
        WarningService.WarningAdded -= OnWarningAdded;
        _autoDismissCts?.Cancel();
        _autoDismissCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
