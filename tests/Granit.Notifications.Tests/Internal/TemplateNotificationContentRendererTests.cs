using System.Text.Json;
using Granit.Notifications.Internal;
using Granit.Notifications.Rendering;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Internal;

public sealed class TemplateNotificationContentRendererTests
{
    private static TemplateNotificationContentRenderer CreateSut(IServiceProvider? sp = null) =>
        new(sp ?? Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<TemplateNotificationContentRenderer>>());

    private static NotificationDeliveryContext BuildContext() => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    [Theory]
    [InlineData(NotificationContentFormat.PlainText)]
    [InlineData(NotificationContentFormat.TitleBody)]
    [InlineData(NotificationContentFormat.Markdown)]
    public async Task TextFormats_ReturnNull_UntilTextTemplateSupportLands(NotificationContentFormat format) =>
        (await CreateSut().RenderAsync(BuildContext(), recipient: null, format, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBeNull();

    [Fact]
    public async Task Html_WithoutTemplating_ReturnsNull_SoChannelsKeepTheirFallback() =>
        (await CreateSut().RenderAsync(BuildContext(), recipient: null, NotificationContentFormat.Html, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBeNull();

    [Fact]
    public void ExtractAndStripTitle_ExtractsAndRemovesTheTag()
    {
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle(
            "<title>Hello</title>\n<p>Body</p>");

        title.ShouldBe("Hello");
        body.ShouldBe("<p>Body</p>");
    }

    [Fact]
    public void ExtractAndStripTitle_NoTitle_ReturnsNullAndOriginalBody()
    {
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle("<p>Body</p>");

        title.ShouldBeNull();
        body.ShouldBe("<p>Body</p>");
    }
}
