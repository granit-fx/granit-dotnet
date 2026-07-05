// =============================================================================
// Tests - EfCoreNotificationSubscriptionStore
// =============================================================================
// Verifies subscription operations: topic subscribe/unsubscribe, subscriber IDs,
// entity follow/unfollow, entity followers list.
// =============================================================================

using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreNotificationSubscriptionStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreNotificationSubscriptionStore _store;

    public EfCoreNotificationSubscriptionStoreTests()
    {
        _store = new EfCoreNotificationSubscriptionStore(_factory, Substitute.For<ICurrentTenant>(), new SimpleGuidGenerator());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SubscribeAsync_InsertsSubscription()
    {
        const string userId = "user-sub";
        const string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> subscribers = await _store.GetSubscriberIdsAsync(typeName, tenantId, TestContext.Current.CancellationToken);
        subscribers.ShouldContain(userId);
    }

    [Fact]
    public async Task SubscribeAsync_DuplicateSubscription_DoesNotInsertTwice()
    {
        const string userId = "user-dup";
        const string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationSubscription> subscriptions = await _store.GetUserSubscriptionsAsync(userId, tenantId, TestContext.Current.CancellationToken);
        subscriptions.Count(s => s.NotificationTypeName == typeName && s.EntityType == null).ShouldBe(1);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesSubscription()
    {
        const string userId = "user-unsub";
        const string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.UnsubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> subscribers = await _store.GetSubscriberIdsAsync(typeName, tenantId, TestContext.Current.CancellationToken);
        subscribers.ShouldNotContain(userId);
    }

    [Fact]
    public async Task GetSubscriberIdsAsync_ReturnsSubscribers()
    {
        const string typeName = "invoice.paid";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync("user-a", typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.SubscribeAsync("user-b", typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.SubscribeAsync("user-c", "other.type", tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> result = await _store.GetSubscriberIdsAsync(typeName, tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("user-a");
        result.ShouldContain("user-b");
    }

    [Fact]
    public async Task FollowEntityAsync_InsertsEntitySubscription()
    {
        const string userId = "user-follow";
        const string entityType = "Order";
        const string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        bool isFollowing = await _store.IsFollowingEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        isFollowing.ShouldBeTrue();
    }

    [Fact]
    public async Task FollowEntityAsync_DuplicateFollow_DoesNotInsertTwice()
    {
        const string userId = "user-dup-follow";
        const string entityType = "Order";
        const string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationSubscription> followers = await _store.GetEntityFollowersAsync(entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        followers.Count(s => s.UserId == userId).ShouldBe(1);
    }

    [Fact]
    public async Task UnfollowEntityAsync_RemovesEntitySubscription()
    {
        const string userId = "user-unfollow";
        const string entityType = "Order";
        const string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.UnfollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        bool isFollowing = await _store.IsFollowingEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        isFollowing.ShouldBeFalse();
    }

    [Fact]
    public async Task GetEntityFollowerIdsAsync_ReturnsFollowers()
    {
        const string entityType = "Project";
        const string entityId = "proj-10";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync("user-x", entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync("user-y", entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync("user-z", entityType, "proj-99", tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> result = await _store.GetEntityFollowerIdsAsync(entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("user-x");
        result.ShouldContain("user-y");
    }

    [Fact]
    public async Task GetEntityFollowersAsync_FiltersByEntityTypeAndId()
    {
        const string entityType = "Task";
        const string entityId = "task-5";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync("user-1", entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync("user-2", entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync("user-3", "OtherType", entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync("user-4", entityType, "task-99", tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationSubscription> result = await _store.GetEntityFollowersAsync(entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(s => s.EntityType == entityType && s.EntityId == entityId);
    }
}
