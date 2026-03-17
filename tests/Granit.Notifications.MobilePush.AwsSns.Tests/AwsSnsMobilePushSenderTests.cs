using Amazon.SimpleNotificationService.Model;
using Granit.Notifications.MobilePush.AwsSns.Internal;
using Granit.Notifications.MobilePush.AwsSns.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.AwsSns.Tests;

public sealed class AwsSnsMobilePushSenderTests
{
    [Fact]
    public void Class_Implements_IMobilePushSender()
    {
        (AwsSnsMobilePushSender sut, _, _) = CreateSender();
        sut.ShouldBeAssignableTo<IMobilePushSender>();
    }

    [Fact]
    public void Class_IsInternal() =>
        typeof(AwsSnsMobilePushSender).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_IsSealed() =>
        typeof(AwsSnsMobilePushSender).IsSealed.ShouldBeTrue();

    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNull()
    {
        (AwsSnsMobilePushSender sut, _, _) = CreateSender();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_CreatesEndpointAndPublishes()
    {
        (AwsSnsMobilePushSender sut, IAwsSnsMobilePushTransport transport, _) = CreateSender();

        transport.CreatePlatformEndpointAsync(Arg.Any<CreatePlatformEndpointRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePlatformEndpointResponse { EndpointArn = "arn:aws:sns:eu-west-1:123:endpoint/GCM/MyApp/token1" });

        transport.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-123" });

        await sut.SendAsync(SimpleMessage("token-abc"), TestContext.Current.CancellationToken);

        await transport.Received(1).CreatePlatformEndpointAsync(
            Arg.Is<CreatePlatformEndpointRequest>(r =>
                r.PlatformApplicationArn == "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp" &&
                r.Token == "token-abc"),
            Arg.Any<CancellationToken>());

        await transport.Received(1).PublishAsync(
            Arg.Is<PublishRequest>(r =>
                r.TargetArn == "arn:aws:sns:eu-west-1:123:endpoint/GCM/MyApp/token1" &&
                r.MessageStructure == "json"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_MultipleTokens_PublishesToEach()
    {
        (AwsSnsMobilePushSender sut, IAwsSnsMobilePushTransport transport, _) = CreateSender();

        transport.CreatePlatformEndpointAsync(Arg.Any<CreatePlatformEndpointRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePlatformEndpointResponse { EndpointArn = "arn:endpoint" });

        transport.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg" });

        MobilePushMessage message = new()
        {
            DeviceTokens = ["token-1", "token-2", "token-3"],
            Title = "Test",
            Body = "Hello",
        };

        await sut.SendAsync(message, TestContext.Current.CancellationToken);

        await transport.Received(3).CreatePlatformEndpointAsync(
            Arg.Any<CreatePlatformEndpointRequest>(), Arg.Any<CancellationToken>());
        await transport.Received(3).PublishAsync(
            Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_EndpointDisabled_PublishesInvalidationEvent()
    {
        (AwsSnsMobilePushSender sut, IAwsSnsMobilePushTransport transport, IMobilePushEventPublisher publisher) = CreateSender();

        transport.CreatePlatformEndpointAsync(Arg.Any<CreatePlatformEndpointRequest>(), Arg.Any<CancellationToken>())
            .Returns<CreatePlatformEndpointResponse>(x =>
                throw new EndpointDisabledException("Endpoint is disabled"));

        await sut.SendAsync(SimpleMessage("dead-token"), TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishTokenInvalidatedAsync(
            Arg.Is<MobilePushTokenInvalidated>(e => e.DeviceToken == "dead-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_PayloadContainsGcmNotification()
    {
        (AwsSnsMobilePushSender sut, IAwsSnsMobilePushTransport transport, _) = CreateSender();

        transport.CreatePlatformEndpointAsync(Arg.Any<CreatePlatformEndpointRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreatePlatformEndpointResponse { EndpointArn = "arn:endpoint" });

        PublishRequest? captured = null;
        transport.PublishAsync(Arg.Do<PublishRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg" });

        await sut.SendAsync(SimpleMessage("token"), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Message.ShouldContain("\"GCM\"");
        captured.Message.ShouldContain("\"default\"");
        captured.Message.ShouldContain("Test Title");
    }

    private static MobilePushMessage SimpleMessage(string token) => new()
    {
        DeviceTokens = [token],
        Title = "Test Title",
        Body = "Test Body",
    };

    private static (AwsSnsMobilePushSender Sut, IAwsSnsMobilePushTransport Transport, IMobilePushEventPublisher Publisher) CreateSender()
    {
        IAwsSnsMobilePushTransport transport = Substitute.For<IAwsSnsMobilePushTransport>();
        IMobilePushEventPublisher publisher = Substitute.For<IMobilePushEventPublisher>();

        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
        };

        IOptionsMonitor<AwsSnsMobilePushOptions> monitor = Substitute.For<IOptionsMonitor<AwsSnsMobilePushOptions>>();
        monitor.CurrentValue.Returns(options);

        AwsSnsMobilePushSender sut = new(
            monitor,
            NullLogger<AwsSnsMobilePushSender>.Instance,
            transport,
            publisher);

        return (sut, transport, publisher);
    }
}
