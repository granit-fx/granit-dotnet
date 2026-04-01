using Amazon.SimpleEmailV2.Model;
using Granit.Notifications.Email.AwsSes.Internal;
using Granit.Notifications.Email.AwsSes.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.AwsSes.Tests;

public sealed class AwsSesEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IOptionsMonitor<AwsSesOptions> CreateOptionsMonitor(AwsSesOptions opts)
    {
        IOptionsMonitor<AwsSesOptions> monitor = Substitute.For<IOptionsMonitor<AwsSesOptions>>();
        monitor.CurrentValue.Returns(opts);
        return monitor;
    }

    private static (AwsSesEmailSender Sender, IAwsSesTransport Transport) CreateSender(AwsSesOptions? options = null)
    {
        AwsSesOptions opts = options ?? new AwsSesOptions
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 10,
        };
        IAwsSesTransport transport = Substitute.For<IAwsSesTransport>();
        transport.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = "test-message-id" });

        AwsSesEmailSender sender = new(
            CreateOptionsMonitor(opts),
            NullLogger<AwsSesEmailSender>.Instance,
            () => transport);

        return (sender, transport);
    }

    private static EmailMessage SimpleMessage(string? fromOverride = null, string? plainText = null) =>
        new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            FromEmailOverride = fromOverride,
            PlainTextBody = plainText,
        };

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_IEmailSender()
    {
        (AwsSesEmailSender sender, IAwsSesTransport _) = CreateSender();
        sender.ShouldBeAssignableTo<IEmailSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(AwsSesEmailSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(AwsSesEmailSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_CallsSesTransportWithCorrectRequest()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("noreply@example.com");
        captured.Destination.ToAddresses.ShouldContain("recipient@example.com");
        captured.Content.Simple.Subject.Data.ShouldBe("Test subject");
        captured.Content.Simple.Body.Html.Data.ShouldBe("<p>Hello</p>");
    }

    [Fact]
    public async Task SendAsync_DisposesTransportAfterSend()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        transport.Received(1).Dispose();
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithFromEmailOverride_UsesSenderAddressFromEmailOverride()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(fromOverride: "custom@example.com"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("custom@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromEmailOverride_UsesDefaultSenderEmail()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "sender@example.com",
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("sender@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromEmailOverrideOrDefaultSenderEmail_FallsBackToNoreply()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = null,
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("noreply@localhost");
    }

    // -------------------------------------------------------------------------
    // Body construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithPlainTextBody_SetsTextContent()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: "Hello plain"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.Simple.Body.Text.Data.ShouldBe("Hello plain");
    }

    [Fact]
    public async Task SendAsync_WithNullPlainTextBody_DoesNotSetTextContent()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: null),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.Simple.Body.Text.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Configuration set
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithConfigurationSetName_IncludesItInRequest()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "sender@example.com",
            ConfigurationSetName = "my-tracking-set",
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ConfigurationSetName.ShouldBe("my-tracking-set");
    }

    [Fact]
    public async Task SendAsync_WithoutConfigurationSetName_DoesNotSetIt()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "sender@example.com",
            ConfigurationSetName = null,
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ConfigurationSetName.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Logger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsEmailSent()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-central-1",
            DefaultSenderEmail = "sender@example.com",
            TimeoutSeconds = 5,
        };
        IAwsSesTransport transport = Substitute.For<IAwsSesTransport>();
        transport.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = "test-id" });

        ILogger<AwsSesEmailSender> loggerSub = Substitute.For<ILogger<AwsSesEmailSender>>();
        loggerSub.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        AwsSesEmailSender sender = new(
            CreateOptionsMonitor(opts),
            loggerSub,
            () => transport);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        loggerSub.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("rec***@example.com")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // -------------------------------------------------------------------------
    // Custom headers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithHeaders_BuildsMessageHeaders()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        EmailMessage message = new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            Headers = new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = "<https://example.com/unsub>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            },
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.Simple.Headers.ShouldNotBeNull();
        captured.Content.Simple.Headers.Count.ShouldBe(2);
        captured.Content.Simple.Headers.ShouldContain(h =>
            h.Name == "List-Unsubscribe" && h.Value == "<https://example.com/unsub>");
        captured.Content.Simple.Headers.ShouldContain(h =>
            h.Name == "List-Unsubscribe-Post" && h.Value == "List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task SendAsync_WithoutHeaders_DoesNotSetMessageHeaders()
    {
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender();
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.Simple.Headers.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Sender name formatting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithDefaultSenderName_FormatsRfc5322From()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "noreply@example.com",
            DefaultSenderName = "My App",
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("\"My App\" <noreply@example.com>");
    }

    [Fact]
    public async Task SendAsync_WithFromNameOverride_UsesOverrideName()
    {
        AwsSesOptions opts = new()
        {
            Region = "eu-west-1",
            DefaultSenderEmail = "noreply@example.com",
            DefaultSenderName = "Default",
            TimeoutSeconds = 5,
        };
        (AwsSesEmailSender sender, IAwsSesTransport transport) = CreateSender(opts);
        SendEmailRequest? captured = null;
        await transport.SendEmailAsync(
            Arg.Do<SendEmailRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        EmailMessage message = new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            FromNameOverride = "Override Name",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.FromEmailAddress.ShouldBe("\"Override Name\" <noreply@example.com>");
    }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullFactory_Throws()
    {
        AwsSesOptions opts = new() { Region = "eu-west-1" };

        Should.Throw<ArgumentNullException>(() =>
            new AwsSesEmailSender(
                CreateOptionsMonitor(opts),
                NullLogger<AwsSesEmailSender>.Instance,
                transportFactory: null));
    }
}
