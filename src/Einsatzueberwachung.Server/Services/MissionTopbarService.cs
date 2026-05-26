using Microsoft.AspNetCore.Components;

namespace Einsatzueberwachung.Server.Services;

public sealed class MissionTopbarService
{
    public event Action? Changed;

    public RenderFragment? Content { get; private set; }

    private object? _owner;

    public void SetContent(object owner, RenderFragment? content)
    {
        _owner = owner;
        Content = content;
        Changed?.Invoke();
    }

    public void ClearContent(object owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        _owner = null;
        Content = null;
        Changed?.Invoke();
    }
}
