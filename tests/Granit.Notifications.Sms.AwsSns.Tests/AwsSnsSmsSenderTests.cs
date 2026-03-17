using Amazon.SimpleNotificationService.Model;
using Granit.Notifications.Sms.AwsSns.Internal;
using Granit.Notifications.Sms.AwsSns.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.AwsSns.Tests;

public sealed class AwsSnsSmsSenderTests
{
    [Fact]
    public void Class_Implements_ISmsSender()
    {
        (AwsSnsSmsSender sut, _) = CreateSender();
        sut.ShouldBeAssignableTo<ISmsSender>();
    }

    [Fact]
    public void Class_IsInternal() =>
        typeof(AwsSnsSmsSender).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_IsSealed() =>
        typeof(AwsSnsSmsSender).IsSealed.ShouldBeTrue();

    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNull()
    {
        (AwsSnsSmsSender sut, _) = CreateSender();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_CallsTransportWithCorrectPhoneNumber()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender();

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.PhoneNumber.ShouldBe("+15559876543");
        captured.Message.ShouldBe("Test SMS body");
    }

    [Fact]
    public async Task SendAsync_SetsSmsTypeAttribute()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender();

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.MessageAttributes.ShouldContainKey("AWS.SNS.SMS.SMSType");
        captured.MessageAttributes["AWS.SNS.SMS.SMSType"].StringValue.ShouldBe("Transactional");
    }

    [Fact]
    public async Task SendAsync_WithSenderId_SetsSenderIdAttribute()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender(senderId: "MySender");

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.MessageAttributes.ShouldContainKey("AWS.SNS.SMS.SenderID");
        captured.MessageAttributes["AWS.SNS.SMS.SenderID"].StringValue.ShouldBe("MySender");
    }

    [Fact]
    public async Task SendAsync_MessageSenderIdOverridesOptions()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender(senderId: "OptSender");

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        SmsMessage message = new()
        {
            To = "+15559876543",
            Body = "Test",
            SenderId = "MsgSender",
        };

        await sut.SendAsync(message, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.MessageAttributes["AWS.SNS.SMS.SenderID"].StringValue.ShouldBe("MsgSender");
    }

    [Fact]
    public async Task SendAsync_WithOriginationNumber_SetsAttribute()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender(originationNumber: "+15551234567");

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.MessageAttributes.ShouldContainKey("AWS.MM.SMS.OriginationNumber");
        captured.MessageAttributes["AWS.MM.SMS.OriginationNumber"].StringValue.ShouldBe("+15551234567");
    }

    [Fact]
    public async Task SendAsync_WithoutSenderId_DoesNotSetSenderIdAttribute()
    {
        (AwsSnsSmsSender sut, IAwsSnsSmsTransport transport) = CreateSender();

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.MessageAttributes.ShouldNotContainKey("AWS.SNS.SMS.SenderID");
    }

    private static SmsMessage SimpleMessage() => new()
    {
        To = "+15559876543",
        Body = "Test SMS body",
    };

    private static (AwsSnsSmsSender Sut, IAwsSnsSmsTransport Transport) CreateSender(
        string? senderId = null,
        string? originationNumber = null)
    {
        IAwsSnsSmsTransport transport = Substitute.For<IAwsSnsSmsTransport>();

        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
            SenderId = senderId,
            OriginationNumber = originationNumber,
        };

        IOptionsMonitor<AwsSnsSmsOptions> monitor = Substitute.For<IOptionsMonitor<AwsSnsSmsOptions>>();
        monitor.CurrentValue.Returns(options);

        AwsSnsSmsSender sut = new(
            monitor,
            NullLogger<AwsSnsSmsSender>.Instance,
            transport);

        return (sut, transport);
    }
}
