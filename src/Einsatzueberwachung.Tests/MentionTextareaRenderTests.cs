using Einsatzueberwachung.Server.Components;

namespace Einsatzueberwachung.Tests;

public class MentionTextareaRenderTests
{
    [Fact]
    public void RenderWithMentions_EscapedDelimiters_AreRenderedAsPlainText()
    {
        var text = "Pruefung @[Hund:Rex \\| Alpha \\] 1|dog-1] erfolgreich";

        var html = MentionTextarea.RenderWithMentions(text).Value;

        Assert.Contains("Rex | Alpha ] 1", html, StringComparison.Ordinal);
        Assert.Contains("/stammdaten?tab=dogs&amp;highlight=dog-1", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderWithMentions_LegacyTypedToken_StillRenders()
    {
        var text = "Hinweis @[Team:Alpha]";

        var html = MentionTextarea.RenderWithMentions(text).Value;

        Assert.Contains("Alpha", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderWithMentions_LegacyUntypedToken_StillRenders()
    {
        var text = "Hinweis @[Irgendein Name]";

        var html = MentionTextarea.RenderWithMentions(text).Value;

        Assert.Contains("Irgendein Name", html, StringComparison.Ordinal);
        Assert.Contains("mention-badge", html, StringComparison.Ordinal);
    }
}
