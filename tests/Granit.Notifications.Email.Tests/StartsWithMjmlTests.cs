using Granit.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

/// <summary>
/// Covers <see cref="TemplateNotificationContentRenderer.StartsWithMjml"/>: the layout-wrapping
/// decision that injects MJML bodies raw at the section level vs. wrapping plain HTML in a
/// default <c>mj-text</c>. The notable case is a leading <c>&lt;!-- AUTO-TRANSLATED --&gt;</c>
/// marker (or any HTML comment) which must be skipped so a translated MJML fragment is not
/// mistaken for plain HTML.
/// </summary>
public sealed class StartsWithMjmlTests
{
    [Fact]
    public void MjmlTag_ReturnsTrue() =>
        TemplateNotificationContentRenderer.StartsWithMjml("<mj-text>x</mj-text>").ShouldBeTrue();

    [Fact]
    public void LeadingWhitespace_ReturnsTrue() =>
        TemplateNotificationContentRenderer.StartsWithMjml("   \n  <mj-section>").ShouldBeTrue();

    [Fact]
    public void LeadingAutoTranslatedComment_ReturnsTrue() =>
        TemplateNotificationContentRenderer.StartsWithMjml("<!-- AUTO-TRANSLATED -->\n<mj-text>x</mj-text>").ShouldBeTrue();

    [Fact]
    public void MultipleLeadingComments_ReturnsTrue() =>
        TemplateNotificationContentRenderer.StartsWithMjml("<!-- a --> <!-- b --><mj-button>").ShouldBeTrue();

    [Fact]
    public void PlainHtml_ReturnsFalse() =>
        TemplateNotificationContentRenderer.StartsWithMjml("<p>hello</p>").ShouldBeFalse();

    [Fact]
    public void CommentThenPlainHtml_ReturnsFalse() =>
        TemplateNotificationContentRenderer.StartsWithMjml("<!-- c --><p>hi</p>").ShouldBeFalse();
}
