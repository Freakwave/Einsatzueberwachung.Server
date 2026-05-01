using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Einsatzueberwachung.Server.Components;

/// <summary>
/// Represents a single entry in the @-mention suggestion dropdown.
/// </summary>
public record MentionSuggestion(string Id, string DisplayName, MentionType Type);

/// <summary>
/// Category of a mention suggestion.
/// </summary>
public enum MentionType
{
    Team,
    Person,
    Hund
}

/// <summary>
/// A textarea component with @-mention autocomplete.
/// When the user types "@" followed by any characters, a dropdown of
/// matching teams, persons, and dogs is shown. Selecting an item inserts
/// "@[DisplayName]" into the text.
/// </summary>
public partial class MentionTextarea : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Two-way bound text value.</summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    /// <summary>Raised whenever the text changes.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>All available mention candidates (teams, persons, dogs).</summary>
    [Parameter] public IReadOnlyList<MentionSuggestion> Mentions { get; set; } = Array.Empty<MentionSuggestion>();

    /// <summary>Number of rows for the textarea.</summary>
    [Parameter] public int Rows { get; set; } = 3;

    /// <summary>Placeholder text for the textarea.</summary>
    [Parameter] public string Placeholder { get; set; } = string.Empty;

    /// <summary>Additional CSS class(es) applied to the textarea element.</summary>
    [Parameter] public string AdditionalClass { get; set; } = string.Empty;

    private ElementReference _textareaRef;
    private DotNetObjectReference<MentionTextarea>? _dotNetRef;

    private bool _showDropdown;
    private int _selectedIndex;
    private List<MentionSuggestion> _filteredSuggestions = new();
    private int _atPosition = -1;
    private string _mentionQuery = string.Empty;
    private bool _jsInitialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("mentionTextarea.init", _textareaRef, _dotNetRef);
            _jsInitialized = true;
        }
    }

    private async Task HandleInputAsync(ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? string.Empty;
        await ValueChanged.InvokeAsync(text);

        var caretPos = _jsInitialized
            ? await JS.InvokeAsync<int>("mentionTextarea.getCaretPosition", _textareaRef)
            : text.Length;

        var atIdx = FindAtIndex(text, caretPos);
        if (atIdx >= 0)
        {
            _atPosition = atIdx;
            _mentionQuery = text.Substring(atIdx + 1, caretPos - atIdx - 1);
            FilterSuggestions();
            _showDropdown = _filteredSuggestions.Count > 0;
            _selectedIndex = 0;
        }
        else
        {
            CloseSuggestions();
        }

        if (_jsInitialized)
            await JS.InvokeVoidAsync("mentionTextarea.setDropdownOpen", _textareaRef, _showDropdown);
    }

    private void HandleBlur()
    {
        // Delay closing so a mousedown on a suggestion item fires first.
        // The mousedown uses :preventDefault to keep textarea focus, so in practice
        // blur doesn't fire during a click, but we keep this as a safety net.
        _ = CloseAfterDelayAsync();
    }

    private async Task CloseAfterDelayAsync()
    {
        await Task.Delay(200);
        await InvokeAsync(() =>
        {
            if (_showDropdown)
                CloseSuggestions();
        });
    }

    private async Task SelectSuggestionAsync(MentionSuggestion suggestion)
    {
        var mentionText = $"@[{suggestion.DisplayName}]";
        var endOfQuery = _atPosition + 1 + _mentionQuery.Length;

        var beforeAt = _atPosition > 0 ? Value[.._atPosition] : string.Empty;
        var afterQuery = endOfQuery < Value.Length ? Value[endOfQuery..] : string.Empty;

        var newValue = beforeAt + mentionText + " " + afterQuery;
        var newCaret = _atPosition + mentionText.Length + 1; // +1 for trailing space

        await ValueChanged.InvokeAsync(newValue);
        if (_jsInitialized)
            await JS.InvokeVoidAsync("mentionTextarea.setValueAndCaret", _textareaRef, newValue, newCaret);
        CloseSuggestions();
    }

    private void CloseSuggestions()
    {
        _showDropdown = false;
        _filteredSuggestions.Clear();
        _selectedIndex = 0;
        _atPosition = -1;
        _mentionQuery = string.Empty;
        StateHasChanged();
    }

    // ── JS-invokable methods called from the keydown interceptor ────────────

    [JSInvokable]
    public Task KeyboardNavigateAsync(string key)
    {
        if (!_showDropdown || _filteredSuggestions.Count == 0)
            return Task.CompletedTask;

        _selectedIndex = key switch
        {
            "ArrowDown" => Math.Min(_selectedIndex + 1, _filteredSuggestions.Count - 1),
            "ArrowUp" => Math.Max(_selectedIndex - 1, 0),
            _ => _selectedIndex
        };

        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task SelectCurrentAsync()
    {
        if (!_showDropdown || _filteredSuggestions.Count == 0)
            return;

        await SelectSuggestionAsync(_filteredSuggestions[_selectedIndex]);
    }

    [JSInvokable]
    public Task EscapeAsync()
    {
        CloseSuggestions();
        return Task.CompletedTask;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void FilterSuggestions()
    {
        var query = _mentionQuery.Trim();
        _filteredSuggestions = Mentions
            .Where(m => string.IsNullOrEmpty(query) ||
                        m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Scans backwards from <paramref name="caretPos"/> to find the start of an
    /// unfinished @mention (i.e. an @ that has not yet been closed with "]").
    /// Returns the index of "@", or -1 if the cursor is not inside a mention query.
    /// </summary>
    private static int FindAtIndex(string text, int caretPos)
    {
        for (int i = caretPos - 1; i >= 0; i--)
        {
            if (text[i] == ']') break; // cursor is after a completed @[...] mention
            if (text[i] == '@')
            {
                // Make sure we have not already entered the "[...]" part
                var between = text[i..caretPos];
                if (!between.Contains('['))
                    return i;
                break;
            }
        }
        return -1;
    }

    internal static string GetSuggestionIcon(MentionType type) => type switch
    {
        MentionType.Team => "bi-people-fill",
        MentionType.Person => "bi-person-fill",
        MentionType.Hund => "bi-heart-fill",
        _ => "bi-at"
    };

    internal static string GetTypeLabel(MentionType type) => type switch
    {
        MentionType.Team => "Team",
        MentionType.Person => "Person",
        MentionType.Hund => "Hund",
        _ => string.Empty
    };

    internal static string GetTypeCssModifier(MentionType type) => type.ToString().ToLowerInvariant();

    // ── Rendering helpers (used by note display) ────────────────────────────

    /// <summary>
    /// Parses <c>@[Name]</c> tokens inside <paramref name="text"/> and returns
    /// an HTML fragment in which each token is replaced by a styled badge span.
    /// All other text is HTML-encoded to prevent injection.
    /// </summary>
    public static Microsoft.AspNetCore.Components.MarkupString RenderWithMentions(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Microsoft.AspNetCore.Components.MarkupString(string.Empty);

        var encoder = System.Text.Encodings.Web.HtmlEncoder.Default;
        var sb = new System.Text.StringBuilder();
        var regex = MentionPattern();
        int lastIdx = 0;

        foreach (System.Text.RegularExpressions.Match match in regex.Matches(text))
        {
            sb.Append(encoder.Encode(text[lastIdx..match.Index]));
            var name = encoder.Encode(match.Groups[1].Value);
            sb.Append($"<span class=\"mention-badge\"><i class=\"bi bi-at\"></i>{name}</span>");
            lastIdx = match.Index + match.Length;
        }

        sb.Append(encoder.Encode(text[lastIdx..]));
        return new Microsoft.AspNetCore.Components.MarkupString(sb.ToString());
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"@\[([^\]\s][^\]]*)\]")]
    private static partial System.Text.RegularExpressions.Regex MentionPattern();

    public async ValueTask DisposeAsync()
    {
        if (_jsInitialized)
        {
            try
            {
                await JS.InvokeVoidAsync("mentionTextarea.dispose", _textareaRef);
            }
            catch (JSDisconnectedException) { /* circuit closed */ }
        }
        _dotNetRef?.Dispose();
    }
}
