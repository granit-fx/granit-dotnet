// =============================================================================
// Tests - EfCoreUserNotificationStore
// =============================================================================
// Verifies CRUD operations on in-app user notifications (inbox):
// insert, get, paginated list, unread count, mark as read, entity feed.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreUserNotificationStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreUserNotificationStore _store;

    public EfCoreUserNotificationStoreTests()
    {
        _store = new EfCoreUserNotificationStore(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task InsertAsync_ThenGetAsync_ReturnsNotification()
    {
        UserNotification notification = BuildNotification();

        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        UserNotification? result = await _store.GetAsync(notification.Id, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(notification.Id);
        result.RecipientUserId.ShouldBe(notification.RecipientUserId);
        result.NotificationTypeName.ShouldBe(notification.NotificationTypeName);
        result.State.ShouldBe(UserNotificationState.Unread);
    }

    [Fact]
    public async Task GetListAsync_ReturnsPaginatedResults_SortedByDate()
    {
        string userId = "user-paginated";
        var tenantId = Guid.NewGuid();
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        // Insert 5 notifications with different timestamps
        for (int i = 0; i < 5; i++)
        {
            UserNotification notification = BuildNotification(recipientUserId: userId, tenantId: tenantId, createdAt: baseTime.AddMinutes(i));
            await _store.InsertAsync(notification, TestContext.Current.CancellationToken);
        }

        // Request page 2 of size 3 (skips first 3, returns next 2)
        PagedResult<UserNotification> result = await _store.GetListAsync(userId, tenantId, page: 2, pageSize: 3, TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(2);
        // Should be sorted descending by CreatedAt
        result.Items[0].CreatedAt.ShouldBeGreaterThanOrEqualTo(result.Items[1].CreatedAt);
    }

    [Fact]
    public async Task GetListAsync_FiltersByRecipientAndTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _store.InsertAsync(BuildNotification(recipientUserId: "user-a", tenantId: tenantA), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification(recipientUserId: "user-a", tenantId: tenantB), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification(recipientUserId: "user-b", tenantId: tenantA), TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result = await _store.GetListAsync("user-a", tenantA, page: 1, pageSize: 100, TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].RecipientUserId.ShouldBe("user-a");
        result.Items[0].TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        string userId = "user-unread-count";
        var tenantId = Guid.NewGuid();

        // Insert 3 unread notifications
        for (int i = 0; i < 3; i++)
        {
            await _store.InsertAsync(BuildNotification(recipientUserId: userId, tenantId: tenantId), TestContext.Current.CancellationToken);
        }

        // Insert 1 read notification via MarkAsRead behavior method
        UserNotification readNotification = BuildNotification(recipientUserId: userId, tenantId: tenantId);
        readNotification.MarkAsRead(DateTimeOffset.UtcNow);
        await _store.InsertAsync(readNotification, TestContext.Current.CancellationToken);

        int count = await _store.GetUnreadCountAsync(userId, tenantId, TestContext.Current.CancellationToken);

        count.ShouldBe(3);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsStateAndReadAt()
    {
        UserNotification notification = BuildNotification();
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        DateTimeOffset readAt = DateTimeOffset.UtcNow;
        await _store.MarkAsReadAsync(notification.Id, readAt, TestContext.Current.CancellationToken);

        UserNotification? result = await _store.GetAsync(notification.Id, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.State.ShouldBe(UserNotificationState.Read);
        (result.ReadAt!.Value - readAt).Duration().ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        string userId = "user-mark-all";
        var tenantId = Guid.NewGuid();

        // Insert 3 unread notifications
        for (int i = 0; i < 3; i++)
        {
            await _store.InsertAsync(BuildNotification(recipientUserId: userId, tenantId: tenantId), TestContext.Current.CancellationToken);
        }

        DateTimeOffset readAt = DateTimeOffset.UtcNow;
        await _store.MarkAllAsReadAsync(userId, tenantId, readAt, TestContext.Current.CancellationToken);

        int unreadCount = await _store.GetUnreadCountAsync(userId, tenantId, TestContext.Current.CancellationToken);
        unreadCount.ShouldBe(0);

        PagedResult<UserNotification> all = await _store.GetListAsync(userId, tenantId, page: 1, pageSize: 100, TestContext.Current.CancellationToken);
        all.Items.ShouldAllBe(n => n.State == UserNotificationState.Read);
    }

    [Fact]
    public async Task GetByEntityAsync_FiltersCorrectly()
    {
        var tenantId = Guid.NewGuid();
        string entityType = "Order";
        string entityId = "order-42";

        await _store.InsertAsync(BuildNotification(tenantId: tenantId, relatedEntityType: entityType, relatedEntityId: entityId), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification(tenantId: tenantId, relatedEntityType: entityType, relatedEntityId: entityId), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification(tenantId: tenantId, relatedEntityType: entityType, relatedEntityId: "order-99"), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification(tenantId: tenantId, relatedEntityType: "Invoice", relatedEntityId: entityId), TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result = await _store.GetByEntityAsync(entityType, entityId, tenantId, page: 1, pageSize: 100, TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(n => n.RelatedEntityType == entityType && n.RelatedEntityId == entityId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static UserNotification BuildNotification(
        string recipientUserId = "user-1",
        Guid? tenantId = null,
        DateTimeOffset? createdAt = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null) =>
        UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            recipientUserId,
            JsonSerializer.SerializeToElement(new { key = "value" }),
            createdAt ?? DateTimeOffset.UtcNow,
            tenantId,
            relatedEntityType,
            relatedEntityId);
}
