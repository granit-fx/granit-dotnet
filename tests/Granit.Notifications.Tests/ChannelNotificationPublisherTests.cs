// =============================================================================
// Tests - ChannelNotificationPublisher
// =============================================================================
// Verifies Channel-backed publisher: trigger construction with explicit
// recipients, entity references, subscriber/follower variants, tenant capture.
// =============================================================================

using System.Threading.Channels;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Notifications.Internal;
using Granit.Notifications.Messages;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class ChannelNotificationPublisherTests
{
    private readonly Channel<NotificationTrigger> _channel = Channel.CreateUnbounded<NotificationTrigger>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock;
    private readonly ChannelNotificationPublisher _publisher;

    public ChannelNotificationPublisherTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);
        _currentTenant.IsAvailable.Returns(false);
        _publisher = new ChannelNotificationPublisher(_channel, _currentTenant, _clock);
    }

    [Fact]
    public async Task PublishAsync_ExplicitRecipients_PublishesTriggerWithRecipients()
    {
        IReadOnlyList<string> recipients = ["user-1", "user-2"];

        await _publisher.PublishAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            recipients,
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.NotificationTypeName.ShouldBe("test.notification");
        trigger.RecipientUserIds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_WithEntityReference_IncludesRelatedEntity()
    {
        EntityReference entity = new("Invoice", "inv-42");

        await _publisher.PublishAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            ["user-1"],
            entity,
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.RelatedEntity.ShouldNotBeNull();
        trigger.RelatedEntity.EntityType.ShouldBe("Invoice");
        trigger.RelatedEntity.EntityId.ShouldBe("inv-42");
    }

    [Fact]
    public async Task PublishToSubscribersAsync_PublishesTriggerWithNoRecipients()
    {
        await _publisher.PublishToSubscribersAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.NotificationTypeName.ShouldBe("test.notification");
        trigger.RecipientUserIds.Count.ShouldBe(0);
        trigger.RelatedEntity.ShouldBeNull();
    }

    [Fact]
    public async Task PublishToEntityFollowersAsync_PublishesTriggerWithEntityReference()
    {
        EntityReference entity = new("Document", "doc-99");

        await _publisher.PublishToEntityFollowersAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            entity,
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.RecipientUserIds.Count.ShouldBe(0);
        trigger.RelatedEntity.ShouldNotBeNull();
        trigger.RelatedEntity.EntityType.ShouldBe("Document");
        trigger.RelatedEntity.EntityId.ShouldBe("doc-99");
    }

    [Fact]
    public async Task PublishAsync_CapturesAmbientTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        await _publisher.PublishAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            ["user-1"],
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task PublishAsync_NoTenant_TenantIdIsNull()
    {
        _currentTenant.IsAvailable.Returns(false);

        await _publisher.PublishAsync(
            TestNotificationType.Instance,
            new TestPayload("value"),
            ["user-1"],
            TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out NotificationTrigger? trigger).ShouldBeTrue();
        trigger!.TenantId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed record TestPayload(string Key);

    private sealed class TestNotificationType : NotificationType<TestPayload>
    {
        public static readonly TestNotificationType Instance = new();
        public override string Name => "test.notification";
        public override IReadOnlyList<string> DefaultChannels => [NotificationChannels.InApp];
    }
}
