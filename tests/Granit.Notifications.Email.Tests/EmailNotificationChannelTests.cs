// =============================================================================
// Tests - EmailNotificationChannel
// =============================================================================
// Verifies the Email channel implementation: email sending via keyed IEmailSender,
// correct field mapping, recipient resolution, and early-return when no email.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email;
using Granit.Notifications.Email.Internal;
using Granit.Notifications.Email.Options;
using Granit.Templating.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailNotificationChannelTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IRecipientResolver _recipientResolver = Substitute.For<IRecipientResolver>();
    private readonly IKeyedServiceProvider _serviceProvider = Substitute.For<IKeyedServiceProvider>();
    private readonly IOptions<EmailChannelOptions> _options;
    private readonly EmailNotificationChannel _channel;

    public EmailNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new EmailChannelOptions
        {
            Provider = "Smtp",
            DefaultSenderEmail = "no-reply@test.com",
        });

        _serviceProvider.GetRequiredKeyedService(typeof(IEmailSender), "Smtp")
            .Returns(_emailSender);

        _channel = new EmailNotificationChannel(
            _serviceProvider,
            _options,
            _recipientResolver,
            new ConfigurationBuilder().Build(),
            Substitute.For<ILogger<EmailNotificationChannel>>());
    }

    [Fact]
    public async Task SendAsync_WithRecipientEmail_SendsEmail()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == "user@test.com" &&
                m.Subject == "test notification" &&
                m.HtmlBody.Contains("test.notification")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullRecipient_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((RecipientInfo?)null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullEmail_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SetsFromEmailOverrideFromOptions()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");
        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.FromEmailOverride.ShouldBe("no-reply@test.com");
    }

    [Fact]
    public void Name_ReturnsEmail() =>
        _channel.Name.ShouldBe(NotificationChannels.Email);

    // ──── Template rendering branch tests ────

    [Fact]
    public async Task SendAsync_WithTemplateRendered_UsesRenderedHtmlAndSubject()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        // Setup template resolver
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "<html><title>Welcome</title><body>Hello {{ model.key }}</body></html>",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        // Setup template engine
        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TextRenderedContent(
                "<html><title>Welcome</title><body>Hello world</body></html>",
                Granit.Templating.Keys.DocumentFormat.Html));

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("Welcome");
        captured.HtmlBody.ShouldContain("Hello world");
    }

    [Fact]
    public async Task SendAsync_NoResolversRegistered_UsesFallbackSubjectAndBody()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns((IEnumerable<ITemplateResolver>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Fallback subject replaces dots with spaces
        captured.Subject.ShouldBe("test notification");
        captured.HtmlBody.ShouldContain("test.notification");
    }

    [Fact]
    public async Task SendAsync_ResolverReturnsNull_UsesFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((Granit.Templating.Pipeline.TemplateDescriptor?)null);

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("test notification");
    }

    [Fact]
    public async Task SendAsync_NoMatchingEngine_UsesFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "template content",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        // No engines registered
        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns((IEnumerable<ITemplateEngine>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("test notification");
    }

    [Fact]
    public async Task SendAsync_EngineCannotRender_UsesFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "template content",
                MimeType = "application/xml",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(false);

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("test notification");
    }

    [Fact]
    public async Task SendAsync_EngineThrows_UsesFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "template content",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Scriban parse error"));

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("test notification");
    }

    [Fact]
    public async Task SendAsync_RenderedHtmlWithoutTitle_UsesRenderedHtmlWithFallbackSubject()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "<html><body>No title here</body></html>",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TextRenderedContent(
                "<html><body>No title here</body></html>",
                Granit.Templating.Keys.DocumentFormat.Html));

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // No <title> in rendered HTML => falls back to notification type name
        captured.Subject.ShouldBe("test notification");
        captured.HtmlBody.ShouldContain("No title here");
    }

    [Fact]
    public async Task SendAsync_EmptyTitleTag_UsesFallbackSubject()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(Arg.Any<Granit.Templating.Keys.TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "<html><title>   </title><body>Body</body></html>",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TextRenderedContent(
                "<html><title>   </title><body>Body</body></html>",
                Granit.Templating.Keys.DocumentFormat.Html));

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Empty <title> after trim => null => fallback
        captured.Subject.ShouldBe("test notification");
    }

    [Fact]
    public async Task SendAsync_EmptyResolverList_UsesFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(Array.Empty<ITemplateResolver>());

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("test notification");
    }

    // ──── Fallback template tests ────

    [Fact]
    public async Task SendAsync_TypeSpecificNotFound_UsesFallbackTemplate()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        // Resolver returns null for type-specific, descriptor for fallback
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(
                Arg.Is<Granit.Templating.Keys.TemplateKey>(k => k.Name == "test.notification"),
                Arg.Any<CancellationToken>())
            .Returns((Granit.Templating.Pipeline.TemplateDescriptor?)null);
        resolver.TryResolveAsync(
                Arg.Is<Granit.Templating.Keys.TemplateKey>(k => k.Name == EmailNotificationChannel.FallbackTemplateName),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "<title>Fallback subject</title><p>Fallback body</p>",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TextRenderedContent(
                "<title>Fallback subject</title><p>Fallback body</p>",
                Granit.Templating.Keys.DocumentFormat.Html));

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Subject.ShouldBe("Fallback subject");
        captured.HtmlBody.ShouldContain("Fallback body");
    }

    [Fact]
    public async Task SendAsync_FallbackTemplate_ReceivesNotificationType()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        // Resolver returns null for type-specific, descriptor for fallback
        ITemplateResolver resolver = Substitute.For<ITemplateResolver>();
        resolver.Priority.Returns(100);
        resolver.TryResolveAsync(
                Arg.Is<Granit.Templating.Keys.TemplateKey>(k => k.Name == "test.notification"),
                Arg.Any<CancellationToken>())
            .Returns((Granit.Templating.Pipeline.TemplateDescriptor?)null);
        resolver.TryResolveAsync(
                Arg.Is<Granit.Templating.Keys.TemplateKey>(k => k.Name == EmailNotificationChannel.FallbackTemplateName),
                Arg.Any<CancellationToken>())
            .Returns(new Granit.Templating.Pipeline.TemplateDescriptor
            {
                Content = "<p>{{ model.notification_type }}</p>",
                MimeType = "text/html",
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateResolver>))
            .Returns(new[] { resolver });

        // Capture the data dict passed to the engine
        Dictionary<string, object?>? capturedData = null;
        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<Granit.Templating.Pipeline.TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<Granit.Templating.Keys.DocumentFormat>(),
                Arg.Any<IReadOnlyList<Granit.Templating.GlobalContext.ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedData = callInfo.Arg<Dictionary<string, object?>>();
                return new Granit.Templating.Pipeline.TextRenderedContent(
                    "<p>test.notification</p>",
                    Granit.Templating.Keys.DocumentFormat.Html);
            });

        _serviceProvider.GetService(typeof(IEnumerable<ITemplateEngine>))
            .Returns(new[] { engine });

        _serviceProvider.GetService(typeof(IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>))
            .Returns((IEnumerable<Granit.Templating.GlobalContext.ITemplateGlobalContext>?)null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        capturedData.ShouldNotBeNull();
        capturedData.ShouldContainKey("notification_type");
        capturedData["notification_type"].ShouldBe("test.notification");
    }

    // ──── ToName and FromNameOverride propagation tests ────

    [Fact]
    public async Task SendAsync_SetsToNameFromRecipientDisplayName()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com", displayName: "John Doe");
        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ToName.ShouldBe("John Doe");
    }

    [Fact]
    public async Task SendAsync_SetsFromNameOverrideFromOptions()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        EmailNotificationChannel channel = CreateChannel(
            opts: new EmailChannelOptions
            {
                Provider = "Smtp",
                DefaultSenderEmail = "no-reply@test.com",
                DefaultSenderName = "My App",
            });

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromNameOverride.ShouldBe("My App");
    }

    // ──── List-Unsubscribe header tests (RFC 8058) ────

    [Fact]
    public async Task SendAsync_WhenAllowUserOptOut_SetsListUnsubscribeHeaders()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        INotificationDefinitionStore defStore = Substitute.For<INotificationDefinitionStore>();
        defStore.Get("test.notification").Returns(new NotificationDefinition("test.notification")
        {
            AllowUserOptOut = true,
            GroupName = "Security",
        });

        EmailNotificationChannel channel = CreateChannel(
            opts: new EmailChannelOptions
            {
                Provider = "Smtp",
                DefaultSenderEmail = "no-reply@test.com",
                UnsubscribeUrl = "https://app.test/prefs",
            },
            defStore: defStore);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.ShouldNotBeNull();
        captured.Headers["List-Unsubscribe"].ShouldBe("<https://app.test/prefs>");
        captured.Headers["List-Unsubscribe-Post"].ShouldBe("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task SendAsync_WhenAllowUserOptOutFalse_DoesNotSetHeaders()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        INotificationDefinitionStore defStore = Substitute.For<INotificationDefinitionStore>();
        defStore.Get("test.notification").Returns(new NotificationDefinition("test.notification")
        {
            AllowUserOptOut = false,
        });

        EmailNotificationChannel channel = CreateChannel(defStore: defStore);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WhenNoDefinitionStore_DefaultsToOptOutAllowed()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        EmailNotificationChannel channel = CreateChannel(
            opts: new EmailChannelOptions
            {
                Provider = "Smtp",
                DefaultSenderEmail = "no-reply@test.com",
                UnsubscribeUrl = "https://app.test/prefs",
            });

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.ShouldNotBeNull();
        captured.Headers["List-Unsubscribe"].ShouldBe("<https://app.test/prefs>");
        captured.Headers["List-Unsubscribe-Post"].ShouldBe("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task SendAsync_WhenNoUnsubscribeUrlConfigured_UsesBaseUrlFallback()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        INotificationDefinitionStore defStore = Substitute.For<INotificationDefinitionStore>();
        defStore.Get("test.notification").Returns(new NotificationDefinition("test.notification")
        {
            AllowUserOptOut = true,
        });

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Granit:Templating:App:BaseUrl"] = "https://myapp.com",
            })
            .Build();

        EmailNotificationChannel channel = CreateChannel(
            config: config,
            defStore: defStore);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.ShouldNotBeNull();
        captured.Headers["List-Unsubscribe"].ShouldBe("<https://myapp.com/notifications/preferences>");
    }

    [Fact]
    public async Task SendAsync_WhenNoUrlResolvable_DoesNotSetHeaders()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        INotificationDefinitionStore defStore = Substitute.For<INotificationDefinitionStore>();
        defStore.Get("test.notification").Returns(new NotificationDefinition("test.notification")
        {
            AllowUserOptOut = true,
        });

        EmailNotificationChannel channel = CreateChannel(defStore: defStore);

        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetupRecipient(string userId, string? email, string? displayName = null) =>
        _recipientResolver.ResolveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo
            {
                UserId = userId,
                Email = email,
                DisplayName = displayName,
            });

    private static NotificationDeliveryContext BuildContext() => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
    };

    private EmailNotificationChannel CreateChannel(
        EmailChannelOptions? opts = null,
        IConfiguration? config = null,
        INotificationDefinitionStore? defStore = null)
    {
        opts ??= new EmailChannelOptions
        {
            Provider = "Smtp",
            DefaultSenderEmail = "no-reply@test.com",
        };

        config ??= new ConfigurationBuilder().Build();

        if (defStore is not null)
        {
            _serviceProvider.GetService(typeof(INotificationDefinitionStore)).Returns(defStore);
        }

        return new EmailNotificationChannel(
            _serviceProvider,
            Microsoft.Extensions.Options.Options.Create(opts),
            _recipientResolver,
            config,
            Substitute.For<ILogger<EmailNotificationChannel>>());
    }
}
