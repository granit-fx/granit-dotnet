using Azure.Communication.Sms;
using Azure.Communication.Sms.Models;
using Granit.Notifications.Sms.AzureCommunicationServices.Internal;
using Granit.Notifications.Sms.AzureCommunicationServices.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.AzureCommunicationServices.Tests;

public sealed class AcsSmsSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SmsSendResult SuccessResult() =>
        SmsModelFactory.SmsSendResult(
            to: "+15559876543",
            messageId: "test-message-id",
            httpStatusCode: 202,
            successful: true,
            errorMessage: null!);

    private static (AcsSmsSender Sender, IAcsSmsTransport Transport) CreateSender(AcsSmsOptions? options = null)
    {
        AcsSmsOptions opts = options ?? new AcsSmsOptions
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 10,
        };
        IAcsSmsTransport transport = Substitute.For<IAcsSmsTransport>();
        transport.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        IOptionsMonitor<AcsSmsOptions> monitor = Substitute.For<IOptionsMonitor<AcsSmsOptions>>();
        monitor.CurrentValue.Returns(opts);

        AcsSmsSender sender = new(
            monitor,
            NullLogger<AcsSmsSender>.Instance,
            transport);

        return (sender, transport);
    }

    private static SmsMessage SimpleMessage(string? senderId = null) =>
        new()
        {
            To = "+15559876543",
            Body = "Hello from tests",
            SenderId = senderId,
        };

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_ISmsSender()
    {
        (AcsSmsSender sender, IAcsSmsTransport _) = CreateSender();
        sender.ShouldBeAssignableTo<ISmsSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(AcsSmsSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(AcsSmsSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — successful send
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_CallsTransportWithCorrectArguments()
    {
        (AcsSmsSender sender, IAcsSmsTransport transport) = CreateSender();
        string? capturedFrom = null;
        string? capturedTo = null;
        string? capturedMessage = null;

        await transport.SendAsync(
            Arg.Do<string>(f => capturedFrom = f),
            Arg.Do<string>(t => capturedTo = t),
            Arg.Do<string>(m => capturedMessage = m),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        capturedFrom.ShouldBe("+15551234567");
        capturedTo.ShouldBe("+15559876543");
        capturedMessage.ShouldBe("Hello from tests");
    }

    // -------------------------------------------------------------------------
    // Sender address resolution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithSenderId_UsesSenderIdFromMessage()
    {
        (AcsSmsSender sender, IAcsSmsTransport transport) = CreateSender();
        string? capturedFrom = null;

        await transport.SendAsync(
            Arg.Do<string>(f => capturedFrom = f),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(
            SimpleMessage(senderId: "+15550001111"),
            TestContext.Current.CancellationToken);

        capturedFrom.ShouldBe("+15550001111");
    }

    [Fact]
    public async Task SendAsync_WithoutSenderId_UsesFromPhoneNumber()
    {
        AcsSmsOptions opts = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "+33612345678",
            TimeoutSeconds = 5,
        };
        (AcsSmsSender sender, IAcsSmsTransport transport) = CreateSender(opts);
        string? capturedFrom = null;

        await transport.SendAsync(
            Arg.Do<string>(f => capturedFrom = f),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        capturedFrom.ShouldBe("+33612345678");
    }

    // -------------------------------------------------------------------------
    // Logger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsSmsSent()
    {
        AcsSmsOptions opts = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 5,
        };
        IAcsSmsTransport transport = Substitute.For<IAcsSmsTransport>();
        transport.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessResult());

        ILogger<AcsSmsSender> loggerSub = Substitute.For<ILogger<AcsSmsSender>>();
        loggerSub.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        IOptionsMonitor<AcsSmsOptions> monitor = Substitute.For<IOptionsMonitor<AcsSmsOptions>>();
        monitor.CurrentValue.Returns(opts);

        AcsSmsSender sender = new(
            monitor,
            loggerSub,
            transport);

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        loggerSub.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("+15559876543")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // -------------------------------------------------------------------------
    // Null message
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        (AcsSmsSender sender, IAcsSmsTransport _) = CreateSender();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sender.SendAsync(null!, TestContext.Current.CancellationToken));
    }
}
