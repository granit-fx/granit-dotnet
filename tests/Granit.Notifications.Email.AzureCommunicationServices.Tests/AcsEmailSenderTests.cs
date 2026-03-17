using Granit.Notifications.Email.AzureCommunicationServices.Internal;
using Granit.Notifications.Email.AzureCommunicationServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.AzureCommunicationServices.Tests;

public sealed class AcsEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (AcsEmailSender Sender, IAcsEmailTransport Transport) CreateSender(
        AcsEmailOptions? options = null)
    {
        AcsEmailOptions opts = options ?? new AcsEmailOptions
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            SenderAddress = "noreply@example.com",
            TimeoutSeconds = 10,
        };

        IAcsEmailTransport transport = Substitute.For<IAcsEmailTransport>();

        IOptionsMonitor<AcsEmailOptions> monitor = Substitute.For<IOptionsMonitor<AcsEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        AcsEmailSender sender = new(
            transport,
            monitor,
            NullLogger<AcsEmailSender>.Instance);

        return (sender, transport);
    }

    private static EmailMessage SimpleMessage(string? fromOverride = null, string? plainText = null) =>
        new()
        {
            To = "recipient@example.com",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>",
            FromOverride = fromOverride,
            PlainTextBody = plainText,
        };

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_IEmailSender()
    {
        (AcsEmailSender sender, _) = CreateSender();
        sender.ShouldBeAssignableTo<IEmailSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(AcsEmailSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(AcsEmailSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_CallsTransportWithCorrectRequest()
    {
        (AcsEmailSender sender, IAcsEmailTransport transport) = CreateSender();
        Azure.Communication.Email.EmailMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<Azure.Communication.Email.EmailMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.SenderAddress.ShouldBe("noreply@example.com");
        captured.Recipients.To.ShouldContain(r => r.Address == "recipient@example.com");
        captured.Content.Subject.ShouldBe("Test subject");
        captured.Content.Html.ShouldBe("<p>Hello</p>");
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithFromOverride_UsesSenderAddressFromOverride()
    {
        (AcsEmailSender sender, IAcsEmailTransport transport) = CreateSender();
        Azure.Communication.Email.EmailMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<Azure.Communication.Email.EmailMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(fromOverride: "custom@example.com"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.SenderAddress.ShouldBe("custom@example.com");
    }

    [Fact]
    public async Task SendAsync_WithoutFromOverride_UsesSenderAddress()
    {
        AcsEmailOptions opts = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            SenderAddress = "sender@example.com",
            TimeoutSeconds = 5,
        };
        (AcsEmailSender sender, IAcsEmailTransport transport) = CreateSender(opts);
        Azure.Communication.Email.EmailMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<Azure.Communication.Email.EmailMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.SenderAddress.ShouldBe("sender@example.com");
    }

    // -------------------------------------------------------------------------
    // Body construction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithPlainTextBody_SetsPlainTextContent()
    {
        (AcsEmailSender sender, IAcsEmailTransport transport) = CreateSender();
        Azure.Communication.Email.EmailMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<Azure.Communication.Email.EmailMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: "Hello plain"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.PlainText.ShouldBe("Hello plain");
    }

    [Fact]
    public async Task SendAsync_WithNullPlainTextBody_DoesNotSetPlainTextContent()
    {
        (AcsEmailSender sender, IAcsEmailTransport transport) = CreateSender();
        Azure.Communication.Email.EmailMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<Azure.Communication.Email.EmailMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(plainText: null),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.PlainText.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Logger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsEmailSent()
    {
        AcsEmailOptions opts = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            SenderAddress = "sender@example.com",
            TimeoutSeconds = 5,
        };

        IAcsEmailTransport transport = Substitute.For<IAcsEmailTransport>();

        ILogger<AcsEmailSender> loggerSub = Substitute.For<ILogger<AcsEmailSender>>();
        loggerSub.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        IOptionsMonitor<AcsEmailOptions> monitor = Substitute.For<IOptionsMonitor<AcsEmailOptions>>();
        monitor.CurrentValue.Returns(opts);

        AcsEmailSender sender = new(
            transport,
            monitor,
            loggerSub);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        loggerSub.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("recipient@example.com")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
