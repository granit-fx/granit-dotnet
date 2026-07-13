// =============================================================================
// Tests - InMemoryWebPushSubscriptionStore
// =============================================================================
// Verifies the in-memory push subscription store: CRUD operations, endpoint
// deduplication (upsert), tenant isolation, and multi-subscription support.
// =============================================================================

using Granit.Notifications.WebPush.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class InMemoryWebPushSubscriptionStoreTests
{
    private readonly InMemoryWebPushSubscriptionStore _store = new();

    [Fact]
    public async Task SaveAndGet_ReturnsSavedSubscription()
    {
        WebPushSubscriptionInfo subscription = BuildSubscription("https://push.example.com/1");

        await _store.SaveSubscriptionAsync("user-1", subscription, null, TestContext.Current.CancellationToken);
        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example.com/1");
    }

    [Fact]
    public async Task Save_SameEndpoint_UpdatesSubscription()
    {
        WebPushSubscriptionInfo original = BuildSubscription("https://push.example.com/1", auth: "auth-old");
        WebPushSubscriptionInfo updated = BuildSubscription("https://push.example.com/1", auth: "auth-new");

        await _store.SaveSubscriptionAsync("user-1", original, null, TestContext.Current.CancellationToken);
        await _store.SaveSubscriptionAsync("user-1", updated, null, TestContext.Current.CancellationToken);
        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Auth.ShouldBe("auth-new");
    }

    [Fact]
    public async Task Remove_DeletesSubscription()
    {
        WebPushSubscriptionInfo subscription = BuildSubscription("https://push.example.com/1");
        await _store.SaveSubscriptionAsync("user-1", subscription, null, TestContext.Current.CancellationToken);

        await _store.RemoveSubscriptionAsync("user-1", "https://push.example.com/1", null, TestContext.Current.CancellationToken);
        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_EmptyStore_ReturnsEmpty()
    {
        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_DifferentTenant_ReturnsEmpty()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        WebPushSubscriptionInfo subscription = BuildSubscription("https://push.example.com/1");
        await _store.SaveSubscriptionAsync("user-1", subscription, tenantA, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", tenantB, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Save_MultipleSubscriptions_ReturnsAll()
    {
        WebPushSubscriptionInfo sub1 = BuildSubscription("https://push.example.com/1");
        WebPushSubscriptionInfo sub2 = BuildSubscription("https://push.example.com/2");
        await _store.SaveSubscriptionAsync("user-1", sub1, null, TestContext.Current.CancellationToken);
        await _store.SaveSubscriptionAsync("user-1", sub2, null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> result = await _store.GetSubscriptionsAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static WebPushSubscriptionInfo BuildSubscription(string endpoint, string auth = "auth-key") => new()
    {
        Endpoint = endpoint,
        P256dh = "p256dh-key",
        Auth = auth,
    };
}
