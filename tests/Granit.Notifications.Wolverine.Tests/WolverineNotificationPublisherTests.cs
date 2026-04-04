using System.Text.Json;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Messages;
using Granit.Notifications.Wolverine.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Notifications.Wolverine.Tests;

public sealed class WolverineNotificationPublisherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WolverineNotificationPublisher _sut;

    private static readonly DateTimeOffset _now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public WolverineNotificationPublisherTests()
    {
        _tenant.IsAvailable.Returns(false);
        _clock.Now.Returns(_now);
        _sut = new(_bus, _tenant, _clock);
    }

    [Fact]
    public async Task PublishAsync_WithRecipients_PublishesNotificationTrigger()
    {
        TestNotificationType notifType = new();
        List<string> recipientIds = ["user-1", "user-2"];

        await _sut.PublishAsync(notifType, new TestData("hello"), recipientIds, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t =>
            t.NotificationTypeName == "Test.Notification" &&
            t.RecipientUserIds.SequenceEqual(recipientIds) &&
            t.OccurredAt == _now));
    }

    [Fact]
    public async Task PublishAsync_WithRecipientsAndEntity_PublishesNotificationTriggerWithEntity()
    {
        TestNotificationType notifType = new();
        List<string> recipientIds = ["user-1"];
        EntityReference entity = new("Order", "order-99");

        await _sut.PublishAsync(notifType, new TestData("hello"), recipientIds, entity, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t =>
            t.RelatedEntity == entity &&
            t.RecipientUserIds.SequenceEqual(recipientIds)));
    }

    [Fact]
    public async Task PublishToSubscribersAsync_PublishesWithNoRecipients()
    {
        TestNotificationType notifType = new();

        await _sut.PublishToSubscribersAsync(notifType, new TestData("broadcast"), TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t =>
            t.NotificationTypeName == "Test.Notification" &&
            t.RecipientUserIds.Count == 0));
    }

    [Fact]
    public async Task PublishToEntityFollowersAsync_PublishesWithRelatedEntity()
    {
        TestNotificationType notifType = new();
        EntityReference entity = new("Task", "task-42");

        await _sut.PublishToEntityFollowersAsync(notifType, new TestData("update"), entity, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t =>
            t.RelatedEntity == entity &&
            t.NotificationTypeName == "Test.Notification"));
    }

    [Fact]
    public async Task PublishAsync_WithActiveTenant_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);
        TestNotificationType notifType = new();

        await _sut.PublishAsync(notifType, new TestData("hello"), ["user-1"], TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t => t.TenantId == tenantId));
    }

    [Fact]
    public async Task PublishAsync_DataIsSerializedToJsonElement()
    {
        TestNotificationType notifType = new();
        TestData data = new("serialized-value");

        await _sut.PublishAsync(notifType, data, ["user-1"], TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<NotificationTrigger>(t =>
            t.Data.ValueKind == JsonValueKind.Object));
    }

    private sealed record TestData(string Message);

    private sealed class TestNotificationType : NotificationType<TestData>
    {
        public override string Name => "Test.Notification";
        public override IReadOnlyList<string> DefaultChannels => ["in-app"];
    }
}
