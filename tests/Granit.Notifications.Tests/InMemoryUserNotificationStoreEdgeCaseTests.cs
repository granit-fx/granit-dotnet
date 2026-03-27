using System.Text.Json;
using Granit.Notifications.Domain;
using Granit.Notifications.Internal;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class InMemoryUserNotificationStoreEdgeCaseTests
{
    private readonly InMemoryUserNotificationStore _store = new();

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        UserNotification? result = await _store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_PageZeroOrNegative_ClampedToPageOne()
    {
        UserNotification notification = BuildNotification("user-1");
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: 0, pageSize: 10, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetListAsync_NegativePage_ClampedToPageOne()
    {
        UserNotification notification = BuildNotification("user-1");
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: -5, pageSize: 10, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetListAsync_LargePageSize_ClampedToMaxPageSize()
    {
        UserNotification notification = BuildNotification("user-1");
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: 1, pageSize: 99999, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetListAsync_ZeroPageSize_ClampedToOne()
    {
        UserNotification n1 = BuildNotification("user-1");
        UserNotification n2 = BuildNotification("user-1");
        await _store.InsertAsync(n1, TestContext.Current.CancellationToken);
        await _store.InsertAsync(n2, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: 1, pageSize: 0, TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetListAsync_HasMore_TrueWhenMoreItemsExist()
    {
        UserNotification n1 = BuildNotification("user-1", createdAt: DateTimeOffset.UtcNow);
        UserNotification n2 = BuildNotification("user-1", createdAt: DateTimeOffset.UtcNow.AddMinutes(1));
        UserNotification n3 = BuildNotification("user-1", createdAt: DateTimeOffset.UtcNow.AddMinutes(2));
        await _store.InsertAsync(n1, TestContext.Current.CancellationToken);
        await _store.InsertAsync(n2, TestContext.Current.CancellationToken);
        await _store.InsertAsync(n3, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: 1, pageSize: 2, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_HasMore_FalseWhenNoMoreItems()
    {
        UserNotification n1 = BuildNotification("user-1");
        await _store.InsertAsync(n1, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: null, page: 1, pageSize: 10, TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_IsolatesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        UserNotification notifA = BuildNotification("user-1", tenantId: tenantA);
        UserNotification notifB = BuildNotification("user-1", tenantId: tenantB);
        await _store.InsertAsync(notifA, TestContext.Current.CancellationToken);
        await _store.InsertAsync(notifB, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetListAsync("user-1", tenantId: tenantA, page: 1, pageSize: 10, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
        result.Items[0].TenantId.ShouldBe(tenantA);
    }

    [Fact]
    public async Task GetUnreadCountAsync_IsolatesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _store.InsertAsync(BuildNotification("user-1", tenantId: tenantA), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification("user-1", tenantId: tenantB), TestContext.Current.CancellationToken);

        int count = await _store.GetUnreadCountAsync("user-1", tenantId: tenantA, TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_NonExistentId_DoesNotThrow()
    {
        Func<Task> act = () => _store.MarkAsReadAsync(Guid.NewGuid(), "non-existent-user", DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_NoUnreadNotifications_DoesNotThrow()
    {
        Func<Task> act = () => _store.MarkAllAsReadAsync("user-1", null, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task GetByEntityAsync_PageZero_ClampedToPageOne()
    {
        UserNotification notification = BuildNotification("user-1", relatedEntityType: "Invoice", relatedEntityId: "inv-1");
        await _store.InsertAsync(notification, TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetByEntityAsync("Invoice", "inv-1", tenantId: null, page: 0, pageSize: 10, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetByEntityAsync_IsolatesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _store.InsertAsync(BuildNotification("user-1", tenantId: tenantA, relatedEntityType: "Invoice", relatedEntityId: "inv-1"), TestContext.Current.CancellationToken);
        await _store.InsertAsync(BuildNotification("user-1", tenantId: tenantB, relatedEntityType: "Invoice", relatedEntityId: "inv-1"), TestContext.Current.CancellationToken);

        PagedResult<UserNotification> result =
            await _store.GetByEntityAsync("Invoice", "inv-1", tenantId: tenantA, page: 1, pageSize: 10, TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static UserNotification BuildNotification(
        string userId,
        DateTimeOffset? createdAt = null,
        Guid? tenantId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null) =>
        UserNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test.notification",
            NotificationSeverity.Info,
            userId,
            JsonSerializer.SerializeToElement(new { key = "value" }),
            createdAt ?? DateTimeOffset.UtcNow,
            tenantId,
            relatedEntityType,
            relatedEntityId);
}
