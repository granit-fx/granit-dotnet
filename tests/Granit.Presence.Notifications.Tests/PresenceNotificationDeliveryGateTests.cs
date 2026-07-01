using System.Diagnostics.Metrics;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Domain;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Notifications.Tests;

public sealed class PresenceNotificationDeliveryGateTests
{
    private static (PresenceNotificationDeliveryGate Gate, IPresenceQueryService Query) Create(PresenceStatus status)
    {
        IPresenceQueryService query = Substitute.For<IPresenceQueryService>();
        var snapshot = new PresenceSnapshot(Guid.NewGuid(), status, ManualOverride: null, OverrideUntilUtc: null, LastSeenUtc: DateTimeOffset.UtcNow);
        query.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(snapshot);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        var metrics = new PresenceMetrics(sp.GetRequiredService<IMeterFactory>());

        return (new PresenceNotificationDeliveryGate(query, tenant, metrics), query);
    }

    [Theory]
    [InlineData(NotificationChannels.InApp)]
    [InlineData(NotificationChannels.Email)]
    [InlineData(NotificationChannels.Sms)]
    [InlineData("WhatsApp")]
    [InlineData("Zulip")]
    public async Task Non_push_channels_always_pass_regardless_of_status(string channelName)
    {
        (PresenceNotificationDeliveryGate gate, _) = Create(PresenceStatus.DoNotDisturb);

        bool allowed = await gate.ShouldDeliverAsync(
            Guid.NewGuid().ToString(), "test.notification", channelName, tenantId: null, CancellationToken.None);

        allowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(NotificationChannels.SignalR)]
    [InlineData(NotificationChannels.Sse)]
    [InlineData(NotificationChannels.WebPush)]
    [InlineData(NotificationChannels.MobilePush)]
    public async Task Push_channels_suppressed_when_dnd(string channelName)
    {
        (PresenceNotificationDeliveryGate gate, _) = Create(PresenceStatus.DoNotDisturb);

        bool allowed = await gate.ShouldDeliverAsync(
            Guid.NewGuid().ToString(), "test.notification", channelName, tenantId: null, CancellationToken.None);

        allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(NotificationChannels.SignalR)]
    [InlineData(NotificationChannels.Sse)]
    [InlineData(NotificationChannels.WebPush)]
    [InlineData(NotificationChannels.MobilePush)]
    public async Task Push_channels_suppressed_when_offline(string channelName)
    {
        (PresenceNotificationDeliveryGate gate, _) = Create(PresenceStatus.Offline);

        bool allowed = await gate.ShouldDeliverAsync(
            Guid.NewGuid().ToString(), "test.notification", channelName, tenantId: null, CancellationToken.None);

        allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PresenceStatus.Online)]
    [InlineData(PresenceStatus.Away)]
    [InlineData(PresenceStatus.Busy)]
    public async Task Push_channels_delivered_when_status_not_dnd_or_offline(PresenceStatus status)
    {
        (PresenceNotificationDeliveryGate gate, _) = Create(status);

        bool allowed = await gate.ShouldDeliverAsync(
            Guid.NewGuid().ToString(), "test.notification", NotificationChannels.SignalR, tenantId: null, CancellationToken.None);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Non_guid_user_id_passes_without_lookup()
    {
        (PresenceNotificationDeliveryGate gate, IPresenceQueryService query) = Create(PresenceStatus.DoNotDisturb);

        bool allowed = await gate.ShouldDeliverAsync(
            "external-oidc-sub-12345", "test.notification", NotificationChannels.SignalR, tenantId: null, CancellationToken.None);

        allowed.ShouldBeTrue();
        await query.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
