using System.Text.Json;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Internal;
using Granit.Notifications.MobilePush.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

public sealed class MobilePushNotificationChannelTests
{
    private readonly IMobilePushSender _sender = Substitute.For<IMobilePushSender>();
    private readonly IMobilePushTokenReader _tokenReader = Substitute.For<IMobilePushTokenReader>();
    private readonly IKeyedServiceProvider _serviceProvider = Substitute.For<IKeyedServiceProvider>();
    private readonly IOptions<MobilePushChannelOptions> _options;
    private readonly MobilePushNotificationChannel _channel;

    public MobilePushNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new MobilePushChannelOptions
        {
            Provider = "Fcm",
        });

        _serviceProvider.GetRequiredKeyedService(typeof(IMobilePushSender), "Fcm")
            .Returns(_sender);

        _channel = new MobilePushNotificationChannel(
            _serviceProvider,
            _options,
            _tokenReader,
            NullLogger<MobilePushNotificationChannel>.Instance);
    }

    [Fact]
    public async Task SendAsync_WithRegisteredTokens_SendsPush()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupTokens("user-1", null, [BuildToken("user-1", "token-abc", MobilePlatform.Android)]);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(
            Arg.Is<MobilePushMessage>(m => m.DeviceTokens.Count == 1 && m.DeviceTokens[0] == "token-abc"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNoTokens_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupTokens("user-1", null, []);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _sender.DidNotReceive().SendAsync(Arg.Any<MobilePushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithMultipleTokens_SendsAll()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupTokens("user-1", null, [
            BuildToken("user-1", "token-1", MobilePlatform.Android),
            BuildToken("user-1", "token-2", MobilePlatform.Ios),
        ]);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(Arg.Is<MobilePushMessage>(m => m.DeviceTokens.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Name_ReturnsMobilePush() => _channel.Name.ShouldBe(NotificationChannels.MobilePush);

    private void SetupTokens(string userId, Guid? tenantId, IReadOnlyList<MobilePushToken> tokens) =>
        _tokenReader.GetTokensAsync(userId, tenantId, Arg.Any<CancellationToken>()).Returns(tokens);

    private static MobilePushToken BuildToken(string userId, string deviceToken, MobilePlatform platform) =>
        MobilePushToken.Create(userId, deviceToken, deviceTokenHash: $"hash-{deviceToken}", platform);

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
}
