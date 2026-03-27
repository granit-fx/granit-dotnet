// =============================================================================
// Tests - InMemoryUserNotificationStore
// =============================================================================
// Verifies the in-memory user notification store: insert/get, pagination,
// unread count, mark-as-read (single and bulk), entity filtering.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Domain;
using Granit.Notifications.Internal;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class InMemoryUserNotificationStoreTests
{
    private readonly InMemoryUserNotificationStore _store = new();

    [Fact]
    public async Task InsertAsync_ThenGetAsync_ReturnsNotification()
    {
        UserNotification notification = BuildNotification("user-1");

        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);
        UserNotification? retrieved = await _store.GetAsync(notification.Id, TestContext.Current.CancellationToken);

        retrieved.ShouldNotBeNull();
        retrieved!.Id.ShouldBe(notification.Id);
    }

    [Fact]
    public async Task GetListAsync_ReturnsPaginatedResultsSortedByDate()
    {
        DateTimeOffset baseTime = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        UserNotification oldest = BuildNotification("user-1", createdAt: baseTime);
        UserNotification middle = BuildNotification("user-1", createdAt: baseTime.AddMinutes(1));
        UserNotification newest = BuildNotification("user-1", createdAt: baseTime.AddMinutes(2));

        await _store.InsertAsync(oldest, TestContext.Current.CancellationToken);
        await _store.InsertAsync(middle, TestContext.Current.CancellationToken);
        await _store.InsertAsync(newest, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> results =
            await _store.GetListAsync("user-1", tenantId: null, page: 1, pageSize: 2, TestContext.Current.CancellationToken);

        results.Items.Count.ShouldBe(2);
        results.TotalCount.ShouldBe(3);
        results.Items[0].CreatedAt.ShouldBeGreaterThanOrEqualTo(results.Items[1].CreatedAt,
            "results should be sorted by date descending (newest first)");
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        UserNotification unread1 = BuildNotification("user-1", state: UserNotificationState.Unread);
        UserNotification unread2 = BuildNotification("user-1", state: UserNotificationState.Unread);
        UserNotification read = BuildNotification("user-1", state: UserNotificationState.Read);

        await _store.InsertAsync(unread1, TestContext.Current.CancellationToken);
        await _store.InsertAsync(unread2, TestContext.Current.CancellationToken);
        await _store.InsertAsync(read, TestContext.Current.CancellationToken);

        int count = await _store.GetUnreadCountAsync("user-1", tenantId: null, TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsStateAndReadAt()
    {
        UserNotification notification = BuildNotification("user-1");
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        DateTimeOffset readAt = DateTimeOffset.UtcNow;
        await _store.MarkAsReadAsync(notification.Id, "user-1", readAt, TestContext.Current.CancellationToken);

        UserNotification? updated = await _store.GetAsync(notification.Id, TestContext.Current.CancellationToken);
        updated!.State.ShouldBe(UserNotificationState.Read);
        updated.ReadAt.ShouldBe(readAt);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        UserNotification unread1 = BuildNotification("user-1", state: UserNotificationState.Unread);
        UserNotification unread2 = BuildNotification("user-1", state: UserNotificationState.Unread);
        UserNotification otherUser = BuildNotification("user-2", state: UserNotificationState.Unread);

        await _store.InsertAsync(unread1, TestContext.Current.CancellationToken);
        await _store.InsertAsync(unread2, TestContext.Current.CancellationToken);
        await _store.InsertAsync(otherUser, TestContext.Current.CancellationToken);

        DateTimeOffset readAt = DateTimeOffset.UtcNow;
        await _store.MarkAllAsReadAsync("user-1", tenantId: null, readAt, TestContext.Current.CancellationToken);

        UserNotification? updated1 = await _store.GetAsync(unread1.Id, TestContext.Current.CancellationToken);
        UserNotification? updated2 = await _store.GetAsync(unread2.Id, TestContext.Current.CancellationToken);
        UserNotification? otherUserNotif = await _store.GetAsync(otherUser.Id, TestContext.Current.CancellationToken);

        updated1!.State.ShouldBe(UserNotificationState.Read);
        updated2!.State.ShouldBe(UserNotificationState.Read);
        otherUserNotif!.State.ShouldBe(UserNotificationState.Unread,
            "other user's notifications should not be affected");
    }

    [Fact]
    public async Task GetByEntityAsync_FiltersCorrectly()
    {
        UserNotification matching = BuildNotification("user-1", relatedEntityType: "Invoice", relatedEntityId: "inv-42");
        UserNotification nonMatching = BuildNotification("user-1", relatedEntityType: "Document", relatedEntityId: "doc-1");
        UserNotification noEntity = BuildNotification("user-1");

        await _store.InsertAsync(matching, TestContext.Current.CancellationToken);
        await _store.InsertAsync(nonMatching, TestContext.Current.CancellationToken);
        await _store.InsertAsync(noEntity, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> results =
            await _store.GetByEntityAsync("Invoice", "inv-42", tenantId: null, page: 1, pageSize: 10, TestContext.Current.CancellationToken);

        results.Items.ShouldHaveSingleItem();
        results.TotalCount.ShouldBe(1);
        results.Items[0].Id.ShouldBe(matching.Id);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static UserNotification BuildNotification(
        string userId,
        UserNotificationState state = UserNotificationState.Unread,
        DateTimeOffset? createdAt = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null)
    {
        var notification = UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            userId,
            JsonSerializer.SerializeToElement(new { key = "value" }),
            createdAt ?? DateTimeOffset.UtcNow,
            relatedEntityType: relatedEntityType,
            relatedEntityId: relatedEntityId);

        if (state == UserNotificationState.Read)
        {
            notification.MarkAsRead(DateTimeOffset.UtcNow);
        }

        return notification;
    }
}
