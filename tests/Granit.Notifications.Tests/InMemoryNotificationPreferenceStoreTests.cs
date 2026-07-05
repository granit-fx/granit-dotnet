// =============================================================================
// Tests - InMemoryNotificationPreferenceStore
// =============================================================================
// Verifies the in-memory preference store: get/set preferences, channel enabled
// check with default fallback, update existing preference, tenant isolation.
// =============================================================================

using Granit.Notifications.Domain;
using Granit.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class InMemoryNotificationPreferenceStoreTests
{
    private readonly InMemoryNotificationPreferenceStore _store = new();

    // -------------------------------------------------------------------------
    // GetListAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_ReturnsUserPreferences()
    {
        NotificationPreference pref1 = BuildPreference("user-1", "order.created", "email");
        NotificationPreference pref2 = BuildPreference("user-1", "order.shipped", "sms");
        NotificationPreference otherUser = BuildPreference("user-2", "order.created", "email");

        await _store.SetAsync(pref1, TestContext.Current.CancellationToken);
        await _store.SetAsync(pref2, TestContext.Current.CancellationToken);
        await _store.SetAsync(otherUser, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationPreference> results =
            await _store.GetListAsync("user-1", tenantId: null, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(p => p.UserId == "user-1");
    }

    [Fact]
    public async Task GetListAsync_EmptyStore_ReturnsEmptyList()
    {
        IReadOnlyList<NotificationPreference> results =
            await _store.GetListAsync("user-1", tenantId: null, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSpecificPreference()
    {
        NotificationPreference preference = BuildPreference("user-1", "order.created", "email");
        await _store.SetAsync(preference, TestContext.Current.CancellationToken);

        NotificationPreference? retrieved = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        retrieved.ShouldNotBeNull();
        retrieved!.UserId.ShouldBe("user-1");
        retrieved.NotificationTypeName.ShouldBe("order.created");
        retrieved.ChannelName.ShouldBe("email");
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        NotificationPreference? retrieved = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        retrieved.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // SetAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_CreatesNewPreference()
    {
        NotificationPreference preference = BuildPreference("user-1", "order.created", "email", isEnabled: false);

        await _store.SetAsync(preference, TestContext.Current.CancellationToken);

        NotificationPreference? retrieved = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        retrieved.ShouldNotBeNull();
        retrieved!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingPreference()
    {
        NotificationPreference original = BuildPreference("user-1", "order.created", "email", isEnabled: true);
        await _store.SetAsync(original, TestContext.Current.CancellationToken);

        NotificationPreference updated = BuildPreference("user-1", "order.created", "email", isEnabled: false);
        await _store.SetAsync(updated, TestContext.Current.CancellationToken);

        NotificationPreference? retrieved = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        retrieved.ShouldNotBeNull();
        retrieved!.IsEnabled.ShouldBeFalse("the preference should have been overwritten");
    }

    // -------------------------------------------------------------------------
    // IsChannelEnabledAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsChannelEnabledAsync_ReturnsStoredValue_WhenPreferenceExists()
    {
        NotificationPreference preference = BuildPreference("user-1", "order.created", "email", isEnabled: false);
        await _store.SetAsync(preference, TestContext.Current.CancellationToken);

        bool isEnabled = await _store.IsChannelEnabledAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        isEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_ReturnsTrue_WhenNoPreference()
    {
        bool isEnabled = await _store.IsChannelEnabledAsync(
            "user-1", "order.created", "email", tenantId: null, TestContext.Current.CancellationToken);

        isEnabled.ShouldBeTrue("default should be enabled when no preference is stored");
    }

    // -------------------------------------------------------------------------
    // Tenant isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_IsolatesPreferencesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        NotificationPreference prefTenantA = BuildPreference("user-1", "order.created", "email", tenantId: tenantA);
        NotificationPreference prefTenantB = BuildPreference("user-1", "order.created", "email", tenantId: tenantB);

        await _store.SetAsync(prefTenantA, TestContext.Current.CancellationToken);
        await _store.SetAsync(prefTenantB, TestContext.Current.CancellationToken);

        IReadOnlyList<NotificationPreference> resultsA =
            await _store.GetListAsync("user-1", tenantId: tenantA, TestContext.Current.CancellationToken);
        IReadOnlyList<NotificationPreference> resultsB =
            await _store.GetListAsync("user-1", tenantId: tenantB, TestContext.Current.CancellationToken);

        resultsA.ShouldHaveSingleItem().TenantId.ShouldBe(tenantA);
        resultsB.ShouldHaveSingleItem().TenantId.ShouldBe(tenantB);
    }

    [Fact]
    public async Task GetAsync_IsolatesPreferencesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        NotificationPreference prefTenantA = BuildPreference("user-1", "order.created", "email", isEnabled: false, tenantId: tenantA);
        NotificationPreference prefTenantB = BuildPreference("user-1", "order.created", "email", isEnabled: true, tenantId: tenantB);

        await _store.SetAsync(prefTenantA, TestContext.Current.CancellationToken);
        await _store.SetAsync(prefTenantB, TestContext.Current.CancellationToken);

        NotificationPreference? retrievedA = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: tenantA, TestContext.Current.CancellationToken);
        NotificationPreference? retrievedB = await _store.GetAsync(
            "user-1", "order.created", "email", tenantId: tenantB, TestContext.Current.CancellationToken);

        retrievedA.ShouldNotBeNull();
        retrievedA!.IsEnabled.ShouldBeFalse();
        retrievedB.ShouldNotBeNull();
        retrievedB!.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task IsChannelEnabledAsync_IsolatesByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        NotificationPreference prefTenantA = BuildPreference("user-1", "order.created", "email", isEnabled: false, tenantId: tenantA);
        await _store.SetAsync(prefTenantA, TestContext.Current.CancellationToken);

        bool enabledForA = await _store.IsChannelEnabledAsync(
            "user-1", "order.created", "email", tenantId: tenantA, TestContext.Current.CancellationToken);
        bool enabledForB = await _store.IsChannelEnabledAsync(
            "user-1", "order.created", "email", tenantId: tenantB, TestContext.Current.CancellationToken);

        enabledForA.ShouldBeFalse("tenant A has an explicit disabled preference");
        enabledForB.ShouldBeTrue("tenant B has no preference, so default applies");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationPreference BuildPreference(
        string userId,
        string typeName,
        string channelName,
        bool isEnabled = true,
        Guid? tenantId = null) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationTypeName = typeName,
            ChannelName = channelName,
            IsEnabled = isEnabled,
            TenantId = tenantId,
        };
}
