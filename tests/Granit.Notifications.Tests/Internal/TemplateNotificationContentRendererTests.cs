using System.Text.Json;
using Granit.Notifications.Internal;
using Granit.Notifications.Rendering;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task TextFormats_WithoutTemplating_ReturnNull_SoChannelsKeepTheirFallback(NotificationContentFormat format) =>
        (await CreateSut().RenderAsync(BuildContext(), recipient: null, format, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldBeNull();

    // ──── Text formats with the real Scriban engine + embedded fallback templates ────

    private static ServiceProvider BuildTemplatingHost()
    {
        ServiceCollection services = new();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddSingleton(Substitute.For<Granit.Templating.Store.IDocumentTemplateStoreReader>());
        services.AddSingleton(Substitute.For<Granit.Timing.IClock>());
        services.AddLogging(b => b.AddProvider(new XunitLogProvider()));
        Granit.Templating.Scriban.Extensions.ServiceCollectionExtensions.AddGranitTemplatingWithScriban(services);
        Granit.Notifications.Extensions.NotificationsRenderingServiceCollectionExtensions.AddGranitNotificationContentRenderer(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PlainText_FallbackTemplate_RendersLocalizedTitleAndBody()
    {
        await using ServiceProvider sp = BuildTemplatingHost();
        INotificationContentRenderer renderer = sp.GetRequiredService<INotificationContentRenderer>();

        RenderedNotificationContent? rendered = await renderer.RenderAsync(
            BuildContext(), recipient: null, NotificationContentFormat.PlainText,
            cancellationToken: TestContext.Current.CancellationToken);

        rendered.ShouldNotBeNull();
        rendered.Title.ShouldBe("You have a new notification");
        rendered.Body.ShouldBe("You have received a new notification of type test.notification.");
    }

    [Fact]
    public async Task PlainText_FrenchRecipient_RendersFrenchVariant()
    {
        await using ServiceProvider sp = BuildTemplatingHost();
        INotificationContentRenderer renderer = sp.GetRequiredService<INotificationContentRenderer>();

        RenderedNotificationContent? rendered = await renderer.RenderAsync(
            BuildContext(),
            new RecipientInfo { UserId = "user-1", PreferredCulture = "fr" },
            NotificationContentFormat.PlainText,
            cancellationToken: TestContext.Current.CancellationToken);

        rendered.ShouldNotBeNull();
        rendered.Title.ShouldBe("Vous avez une nouvelle notification");
        rendered.Body.ShouldContain("test.notification");
    }

    [Fact]
    public async Task Markdown_FallbackTemplate_RendersBoldTypeAndSeverity()
    {
        await using ServiceProvider sp = BuildTemplatingHost();
        INotificationContentRenderer renderer = sp.GetRequiredService<INotificationContentRenderer>();

        RenderedNotificationContent? rendered = await renderer.RenderAsync(
            BuildContext(), recipient: null, NotificationContentFormat.Markdown,
            cancellationToken: TestContext.Current.CancellationToken);

        rendered.ShouldNotBeNull();
        rendered.Body.ShouldContain("**test.notification**");
        rendered.Body.ShouldContain("(Info)");
    }

    private sealed class XunitLogProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new XunitLog();
        public void Dispose() { }
        private sealed class XunitLog : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                TestContext.Current.TestOutputHelper?.WriteLine($"[{logLevel}] {formatter(state, exception)} {exception?.Message}");
        }
    }

    [Fact]
    public void SplitFirstLineTitle_SplitsTitleFromBody()
    {
        (string? title, string body) = TemplateNotificationContentRenderer.SplitFirstLineTitle("Title line\nBody line one\nBody line two");

        title.ShouldBe("Title line");
        body.ShouldBe("Body line one\nBody line two");
    }

    [Fact]
    public void SplitFirstLineTitle_SingleLine_IsBodyOnly()
    {
        (string? title, string body) = TemplateNotificationContentRenderer.SplitFirstLineTitle("Just a body");

        title.ShouldBeNull();
        body.ShouldBe("Just a body");
    }

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

    [Fact]
    public void ExtractAndStripTitle_StripsLeadingAutoTranslatedMarker()
    {
        // Machine-translated templates are stamped with a leading marker comment by
        // scripts/translate-templates.py — it must never leak into the rendered email body.
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle(
            "<!-- AUTO-TRANSLATED (model, 2026-01-01) — REVIEW BEFORE PRODUCTION -->\n<title>Bonjour</title>\n<p>Corps</p>");

        title.ShouldBe("Bonjour");
        body.ShouldBe("<p>Corps</p>");
    }

    [Fact]
    public void ExtractAndStripTitle_StripsMultipleLeadingComments()
    {
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle(
            "<!-- one -->\n<!-- two -->\n<title>Hello</title>\n<p>Body</p>");

        title.ShouldBe("Hello");
        body.ShouldBe("<p>Body</p>");
    }

    [Fact]
    public void ExtractAndStripTitle_KeepsNonCommentContentBeforeTitle()
    {
        // Only leading comments/whitespace are stripped — real markup before the title
        // (unusual, but legal) is preserved.
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle(
            "<!-- marker --><meta charset=\"utf-8\"><title>Hello</title>\n<p>Body</p>");

        title.ShouldBe("Hello");
        body.ShouldBe("<meta charset=\"utf-8\"><p>Body</p>");
    }

    [Fact]
    public void ExtractAndStripTitle_CommentsAfterTitle_StayInBody()
    {
        // Comments after the title (e.g. MSO conditionals) are body content, not metadata.
        (string? title, string body) = TemplateNotificationContentRenderer.ExtractAndStripTitle(
            "<title>Hello</title>\n<!--[if mso]><table></table><![endif]--><p>Body</p>");

        title.ShouldBe("Hello");
        body.ShouldBe("<!--[if mso]><table></table><![endif]--><p>Body</p>");
    }
}
