using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Zulip.Internal;
using Granit.Notifications.Zulip.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Zulip.Tests;

public sealed class ZulipNotificationChannelTests
{
    private readonly IZulipSender _sender = Substitute.For<IZulipSender>();
    private readonly IOptions<ZulipChannelOptions> _options;
    private readonly ZulipNotificationChannel _channel;

    public ZulipNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new ZulipChannelOptions { DefaultStream = "test-alerts", DefaultTopic = "system" });
        _channel = new ZulipNotificationChannel(_sender, _options, NullLogger<ZulipNotificationChannel>.Instance);
    }

    [Fact]
    public async Task SendAsync_SendsToDefaultStreamAndTopic()
    {
        NotificationDeliveryContext context = BuildContext();
        ZulipMessage? captured = null;
        _sender.SendAsync(Arg.Any<ZulipMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<ZulipMessage>(); return Task.CompletedTask; });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Type.ShouldBe("stream");
        captured.Stream.ShouldBe("test-alerts");
        captured.Topic.ShouldBe("system");
    }

    [Fact]
    public async Task SendAsync_ContentContainsNotificationType()
    {
        NotificationDeliveryContext context = BuildContext();
        ZulipMessage? captured = null;
        _sender.SendAsync(Arg.Any<ZulipMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<ZulipMessage>(); return Task.CompletedTask; });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Content.ShouldContain("test.notification");
    }

    [Fact]
    public void Name_ReturnsZulip() => _channel.Name.ShouldBe(NotificationChannels.Zulip);

    [Fact]
    public async Task SendAsync_ContentContainsSeverity()
    {
        NotificationDeliveryContext context = BuildContext(NotificationSeverity.Fatal);
        ZulipMessage? captured = null;
        _sender.SendAsync(Arg.Any<ZulipMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<ZulipMessage>(); return Task.CompletedTask; });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Content.ShouldContain("Fatal");
    }

    [Fact]
    public async Task SendAsync_PassesCancellationTokenToSender()
    {
        NotificationDeliveryContext context = BuildContext();

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _sender.Received(1).SendAsync(Arg.Any<ZulipMessage>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_AlwaysSendsStreamType()
    {
        NotificationDeliveryContext context = BuildContext();
        ZulipMessage? captured = null;
        _sender.SendAsync(Arg.Any<ZulipMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<ZulipMessage>(); return Task.CompletedTask; });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Type.ShouldBe("stream");
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(ZulipNotificationChannel).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(ZulipNotificationChannel).IsNotPublic.ShouldBeTrue();

    [Fact]
    public void Class_ImplementsINotificationChannel() =>
        _channel.ShouldBeAssignableTo<INotificationChannel>();

    // -- Logging branch coverage (source-generated [LoggerMessage]) --

    [Fact]
    public async Task SendAsync_LogsWhenLoggingEnabled()
    {
        ILogger<ZulipNotificationChannel> enabledLogger = Substitute.For<ILogger<ZulipNotificationChannel>>();
        enabledLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var channel = new ZulipNotificationChannel(_sender, _options, enabledLogger);
        NotificationDeliveryContext context = BuildContext();

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        enabledLogger.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    [Fact]
    public async Task SendAsync_WithCustomOptions_LogsStreamAndTopic()
    {
        IOptions<ZulipChannelOptions> customOptions = Microsoft.Extensions.Options.Options.Create(new ZulipChannelOptions
        {
            DefaultStream = "custom-stream",
            DefaultTopic = "custom-topic",
        });
        ILogger<ZulipNotificationChannel> enabledLogger = Substitute.For<ILogger<ZulipNotificationChannel>>();
        enabledLogger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var channel = new ZulipNotificationChannel(_sender, customOptions, enabledLogger);
        ZulipMessage? captured = null;
        _sender.SendAsync(Arg.Any<ZulipMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<ZulipMessage>(); return Task.CompletedTask; });
        NotificationDeliveryContext context = BuildContext();

        await channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Stream.ShouldBe("custom-stream");
        captured.Topic.ShouldBe("custom-topic");
        enabledLogger.ReceivedWithAnyArgs().Log(
            default, default, default(object)!, default, default!);
    }

    private static NotificationDeliveryContext BuildContext(
        NotificationSeverity severity = NotificationSeverity.Warning) => new()
        {
            DeliveryId = Guid.NewGuid(),
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            RecipientUserId = "user-1",
            Severity = severity,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            OccurredAt = DateTimeOffset.UtcNow,
        };
}
