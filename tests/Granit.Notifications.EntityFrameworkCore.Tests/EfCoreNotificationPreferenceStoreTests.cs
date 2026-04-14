// =============================================================================
// Tests - EfCoreNotificationPreferenceStore
// =============================================================================
// Verifies user notification preferences: get list, set (insert/update),
// channel enabled check with default-true fallback.
// =============================================================================

using Granit.MultiTenancy;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class EfCoreNotificationPreferenceStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreNotificationPreferenceStore _store;

    public EfCoreNotificationPreferenceStoreTests()
    {
        _store = new EfCoreNotificationPreferenceStore(_factory, Substitute.For<ICurrentTenant>());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetListAsync_ReturnsUserPreferences()
    {
        string userId = "user-prefs";
        var tenantId = Guid.NewGuid();

        NotificationPreference pref1 = BuildPreference(userId: userId, tenantId: tenantId, notificationTypeName: "type-a", channelName: "email");
        NotificationPreference pref2 = BuildPreference(userId: userId, tenantId: tenantId, notificationTypeName: "type-b", channelName: "sms");
        NotificationPreference otherUserPref = BuildPreference(userId: "other-user", tenantId: tenantId, notificationTypeName: "type-a", channelName: "email");

        await _store.SetAsync(pref1, TestContext.Current.CancellationToken);
        await _store.SetAsync(pref2, TestContext.Current.CancellationToken);
        await _store.SetAsync(otherUserPref, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationPreference> result = await _store.GetListAsync(userId, tenantId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.UserId == userId && p.TenantId == tenantId);
    }

    [Fact]
    public async Task SetAsync_InsertsNewPreference()
    {
        string userId = "user-insert";
        var tenantId = Guid.NewGuid();
        NotificationPreference preference = BuildPreference(userId: userId, tenantId: tenantId, isEnabled: false);

        await _store.SetAsync(preference, TestContext.Current.CancellationToken);

        NotificationPreference? result = await _store.GetAsync(userId, preference.NotificationTypeName, preference.ChannelName, tenantId, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.IsEnabled.ShouldBeFalse();
        result.UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingPreference()
    {
        string userId = "user-update";
        var tenantId = Guid.NewGuid();
        string typeName = "order.created";
        string channelName = "email";

        // Insert initial preference (enabled)
        NotificationPreference initial = BuildPreference(userId: userId, tenantId: tenantId, notificationTypeName: typeName, channelName: channelName, isEnabled: true);
        await _store.SetAsync(initial, TestContext.Current.CancellationToken);

        // Update to disabled
        NotificationPreference updated = BuildPreference(userId: userId, tenantId: tenantId, notificationTypeName: typeName, channelName: channelName, isEnabled: false);
        updated.ModifiedAt = DateTimeOffset.UtcNow;
        updated.ModifiedBy = "admin";
        await _store.SetAsync(updated, TestContext.Current.CancellationToken);

        NotificationPreference? result = await _store.GetAsync(userId, typeName, channelName, tenantId, TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_NoPreference_ReturnsTrue()
    {
        bool result = await _store.IsChannelEnabledAsync("nonexistent-user", "some.type", "push", Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeTrue("no preference stored means channel is enabled by default");
    }

    [Fact]
    public async Task IsChannelEnabledAsync_WithPreference_ReturnsStoredValue()
    {
        string userId = "user-channel-check";
        var tenantId = Guid.NewGuid();
        string typeName = "alert.critical";
        string channelName = "sms";

        NotificationPreference preference = BuildPreference(userId: userId, tenantId: tenantId, notificationTypeName: typeName, channelName: channelName, isEnabled: false);
        await _store.SetAsync(preference, TestContext.Current.CancellationToken);

        bool result = await _store.IsChannelEnabledAsync(userId, typeName, channelName, tenantId, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationPreference BuildPreference(
        string userId = "user-1",
        Guid? tenantId = null,
        string notificationTypeName = "test.notification",
        string channelName = "in_app",
        bool isEnabled = true) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationTypeName = notificationTypeName,
            ChannelName = channelName,
            IsEnabled = isEnabled,
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        };
}
