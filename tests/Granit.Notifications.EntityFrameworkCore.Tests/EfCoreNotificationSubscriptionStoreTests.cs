// =============================================================================
// Tests - EfCoreNotificationSubscriptionStore
// =============================================================================
// Verifies subscription operations: topic subscribe/unsubscribe, subscriber IDs,
// entity follow/unfollow, entity followers list.
// =============================================================================

using Granit.Guids;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreNotificationSubscriptionStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreNotificationSubscriptionStore _store;

    public EfCoreNotificationSubscriptionStoreTests()
    {
        NotificationsContextResolver resolver = new(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, _factory);
        _store = new EfCoreNotificationSubscriptionStore(resolver, new SimpleGuidGenerator());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SubscribeAsync_InsertsSubscription()
    {
        string userId = "user-sub";
        string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> subscribers = await _store.GetSubscriberIdsAsync(typeName, tenantId, TestContext.Current.CancellationToken);
        subscribers.ShouldContain(userId);
    }

    [Fact]
    public async Task SubscribeAsync_DuplicateSubscription_DoesNotInsertTwice()
    {
        string userId = "user-dup";
        string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationSubscription> subscriptions = await _store.GetUserSubscriptionsAsync(userId, tenantId, TestContext.Current.CancellationToken);
        subscriptions.Where(s => s.NotificationTypeName == typeName && s.EntityType == null).Count().ShouldBe(1);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesSubscription()
    {
        string userId = "user-unsub";
        string typeName = "order.created";
        var tenantId = Guid.NewGuid();

        await _store.SubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);
        await _store.UnsubscribeAsync(userId, typeName, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<string> subscribers = await _store.GetSubscriberIdsAsync(typeName, tenantId, TestContext.Current.CancellationToken);
        subscribers.ShouldNotContain(userId);
    }

    [Fact]
    public async Task GetSubscriberIdsAsync_ReturnsSubscribers()
    {
        string typeName = "invoice.paid";
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
        string userId = "user-follow";
        string entityType = "Order";
        string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        bool isFollowing = await _store.IsFollowingEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        isFollowing.ShouldBeTrue();
    }

    [Fact]
    public async Task FollowEntityAsync_DuplicateFollow_DoesNotInsertTwice()
    {
        string userId = "user-dup-follow";
        string entityType = "Order";
        string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationSubscription> followers = await _store.GetEntityFollowersAsync(entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        followers.Where(s => s.UserId == userId).Count().ShouldBe(1);
    }

    [Fact]
    public async Task UnfollowEntityAsync_RemovesEntitySubscription()
    {
        string userId = "user-unfollow";
        string entityType = "Order";
        string entityId = "order-42";
        var tenantId = Guid.NewGuid();

        await _store.FollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        await _store.UnfollowEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);

        bool isFollowing = await _store.IsFollowingEntityAsync(userId, entityType, entityId, tenantId, TestContext.Current.CancellationToken);
        isFollowing.ShouldBeFalse();
    }

    [Fact]
    public async Task GetEntityFollowerIdsAsync_ReturnsFollowers()
    {
        string entityType = "Project";
        string entityId = "proj-10";
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
        string entityType = "Task";
        string entityId = "task-5";
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
