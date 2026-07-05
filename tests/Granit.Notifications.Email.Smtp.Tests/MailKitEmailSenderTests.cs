// =============================================================================
// Tests - MailKitEmailSender
// =============================================================================
// Verifies the MailKit SMTP email sender: MimeMessage construction, sender
// address resolution (FromEmailOverride > Username > fallback), body builder,
// authentication branching, and successful send/disconnect flow.
// Uses ISmtpTransportFactory + ISmtpTransport substitutes to avoid real SMTP.
// =============================================================================

using Granit.Notifications.Email.Smtp.Internal;
using Granit.Notifications.Email.Smtp.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Smtp.Tests;

public sealed class MailKitEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (MailKitEmailSender Sender, ISmtpTransport Transport) CreateSender(SmtpOptions? options = null)
    {
        SmtpOptions opts = options ?? new SmtpOptions
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = true,
            TimeoutSeconds = 10,
        };
        ISmtpTransport transport = Substitute.For<ISmtpTransport>();

        IOptionsMonitor<SmtpOptions> monitor = Substitute.For<IOptionsMonitor<SmtpOptions>>();
        monitor.CurrentValue.Returns(opts);

        MailKitEmailSender sender = new(
            monitor,
            NullLogger<MailKitEmailSender>.Instance,
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
        (MailKitEmailSender? sender, ISmtpTransport _) = CreateSender();
        sender.ShouldBeAssignableTo<IEmailSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(MailKitEmailSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(MailKitEmailSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send (full code path)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_ConnectsAuthenticatesSendsAndDisconnects()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = true,
            Username = "user@mail.com",
            Password = "test-password",
            TimeoutSeconds = 15,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        transport.Timeout.ShouldBe(15000);
        await transport.Received(1).ConnectAsync(
            "mail.example.com",
            587,
            MailKit.Security.SecureSocketOptions.StartTls,
            Arg.Any<CancellationToken>());
        await transport.Received(1).AuthenticateAsync(
            "user@mail.com",
            "test-password",
            Arg.Any<CancellationToken>());
        await transport.Received(1).SendAsync(
            Arg.Any<MimeMessage>(),
            Arg.Any<CancellationToken>());
        await transport.Received(1).DisconnectAsync(
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithoutUseSsl_UsesNoneSocketOptions()
    {
        SmtpOptions opts = new()
        {
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.Received(1).ConnectAsync(
            "localhost",
            25,
            MailKit.Security.SecureSocketOptions.None,
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Authentication branching
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithUsernameAndPassword_Authenticates()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = "user",
            Password = "pass",
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.Received(1).AuthenticateAsync("user", "pass", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullUsernameAndPassword_SkipsAuthentication()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = null,
            Password = null,
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.DidNotReceive().AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithUsernameOnly_NullPassword_SkipsAuthentication()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = "user",
            Password = null,
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.DidNotReceive().AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithPasswordOnly_NullUsername_SkipsAuthentication()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = null,
            Password = "pass",
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.DidNotReceive().AuthenticateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithFromEmailOverride_UsesSenderAddressFromEmailOverride()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(fromOverride: "custom@example.com"), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        ((MailboxAddress)captured.From[0]).Address.ShouldBe("custom@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromEmailOverride_UsesUsername()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = "sender@example.com",
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        ((MailboxAddress)captured.From[0]).Address.ShouldBe("sender@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromEmailOverrideOrUsername_FallsBackToNoreply()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            Username = null,
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.From[0].Name.ShouldBe("noreply@localhost");
        ((MailboxAddress)captured.From[0]).Address.ShouldBe("noreply@localhost");
    }

    // -------------------------------------------------------------------------
    // Body construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithPlainTextBody_SetsTextBody()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: "Hello plain"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.TextBody.ShouldBe("Hello plain");
    }

    [Fact]
    public async Task SendAsync_WithNullPlainTextBody_SkipsTextBody()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: null),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.TextBody.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_SetsRecipientAndSubject()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        ((MailboxAddress)captured.To[0]).Address.ShouldBe("recipient@example.com");
        captured.Subject.ShouldBe("Test subject");
    }

    // -------------------------------------------------------------------------
    // Custom headers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithHeaders_AddsMimeHeaders()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        EmailMessage message = new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            Headers = new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = "<https://example.com/unsub>",
            },
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers["List-Unsubscribe"].ShouldBe("<https://example.com/unsub>");
    }

    // -------------------------------------------------------------------------
    // ToName
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithToName_UsesMailboxAddressWithName()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        EmailMessage message = new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            ToName = "John Doe",
        };

        await sender.SendAsync(message, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.To[0].Name.ShouldBe("John Doe");
    }

    // -------------------------------------------------------------------------
    // Sender name formatting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithDefaultSenderName_UsesNameInFrom()
    {
        SmtpOptions opts = new()
        {
            Host = "mail.example.com",
            Port = 587,
            UseSsl = false,
            DefaultSenderName = "My App",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 5,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);
        MimeMessage? captured = null;
        await transport.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.From[0].Name.ShouldBe("My App");
    }

    // -------------------------------------------------------------------------
    // Timeout calculation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_SetsTimeoutInMilliseconds()
    {
        SmtpOptions opts = new()
        {
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            TimeoutSeconds = 42,
        };
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender(opts);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        transport.Timeout.ShouldBe(42000);
    }

    // -------------------------------------------------------------------------
    // Transport disposal
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_DisposesTransportAfterSend()
    {
        (MailKitEmailSender? sender, ISmtpTransport? transport) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        transport.Received(1).Dispose();
    }

    // -------------------------------------------------------------------------
    // Logger message (exercises the source-generated LogEmailSent)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsEmailSent()
    {
        SmtpOptions opts = new()
        {
            Host = "smtp.test.com",
            Port = 465,
            UseSsl = false,
            TimeoutSeconds = 5,
        };
        ISmtpTransport transport = Substitute.For<ISmtpTransport>();

        ILogger<MailKitEmailSender> logger = Substitute.For<ILogger<MailKitEmailSender>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        IOptionsMonitor<SmtpOptions> monitor = Substitute.For<IOptionsMonitor<SmtpOptions>>();
        monitor.CurrentValue.Returns(opts);

        MailKitEmailSender sender = new(monitor, logger, () => transport);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("rec***@example.com")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // -------------------------------------------------------------------------
    // Null transport factory fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullFactory_UsesDefaultFactory()
    {
        SmtpOptions opts = new() { Host = "localhost", Port = 25, UseSsl = false };
        IOptionsMonitor<SmtpOptions> monitor = Substitute.For<IOptionsMonitor<SmtpOptions>>();
        monitor.CurrentValue.Returns(opts);

        MailKitEmailSender sender = new(monitor, NullLogger<MailKitEmailSender>.Instance);

        // Should not throw — default factory is used internally
        sender.ShouldNotBeNull();
    }
}
