using Granit.Notifications.Email.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

/// <summary>
/// Covers <see cref="EmailNotificationChannel.StartsWithMjml"/>: the layout-wrapping
/// decision that injects MJML bodies raw at the section level vs. wrapping plain HTML in a
/// default <c>mj-text</c>. The notable case is a leading <c>&lt;!-- AUTO-TRANSLATED --&gt;</c>
/// marker (or any HTML comment) which must be skipped so a translated MJML fragment is not
/// mistaken for plain HTML.
/// </summary>
public sealed class StartsWithMjmlTests
{
    [Fact]
    public void MjmlTag_ReturnsTrue() =>
        EmailNotificationChannel.StartsWithMjml("<mj-text>x</mj-text>").ShouldBeTrue();

    [Fact]
    public void LeadingWhitespace_ReturnsTrue() =>
        EmailNotificationChannel.StartsWithMjml("   \n  <mj-section>").ShouldBeTrue();

    [Fact]
    public void LeadingAutoTranslatedComment_ReturnsTrue() =>
        EmailNotificationChannel.StartsWithMjml("<!-- AUTO-TRANSLATED -->\n<mj-text>x</mj-text>").ShouldBeTrue();

    [Fact]
    public void MultipleLeadingComments_ReturnsTrue() =>
        EmailNotificationChannel.StartsWithMjml("<!-- a --> <!-- b --><mj-button>").ShouldBeTrue();

    [Fact]
    public void PlainHtml_ReturnsFalse() =>
        EmailNotificationChannel.StartsWithMjml("<p>hello</p>").ShouldBeFalse();

    [Fact]
    public void CommentThenPlainHtml_ReturnsFalse() =>
        EmailNotificationChannel.StartsWithMjml("<!-- c --><p>hi</p>").ShouldBeFalse();
}
