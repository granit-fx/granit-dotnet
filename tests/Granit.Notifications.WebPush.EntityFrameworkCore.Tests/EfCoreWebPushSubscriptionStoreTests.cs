using Granit.MultiTenancy;
using Granit.Notifications.WebPush.EntityFrameworkCore.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies that the EF Core browser push subscription store upserts and removes
/// on the unique <c>Endpoint</c> key and round-trips the encrypted key material.
/// </summary>
public sealed class EfCoreWebPushSubscriptionStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreWebPushSubscriptionStore _store;

    public EfCoreWebPushSubscriptionStoreTests() =>
        _store = new EfCoreWebPushSubscriptionStore(_factory, Substitute.For<ICurrentTenant>());

    public void Dispose() => _factory.Dispose();

    private static WebPushSubscriptionInfo Sub(string endpoint, string p256dh = "key-p", string auth = "key-a", long? exp = null) =>
        new() { Endpoint = endpoint, P256dh = p256dh, Auth = auth, ExpirationTime = exp };

    [Fact]
    public async Task SaveSubscriptionAsync_New_PersistsAllFields()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1", "p-1", "a-1", 123L), tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.ShouldHaveSingleItem();
        subs[0].Endpoint.ShouldBe("https://push/1");
        subs[0].P256dh.ShouldBe("p-1");
        subs[0].Auth.ShouldBe("a-1");
        subs[0].ExpirationTime.ShouldBe(123L);
    }

    [Fact]
    public async Task SaveSubscriptionAsync_SameEndpoint_UpsertsInPlace()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1", "p-old", "a-old"), tenantId: null, TestContext.Current.CancellationToken);
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1", "p-new", "a-new"), tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.ShouldHaveSingleItem();
        subs[0].P256dh.ShouldBe("p-new");
        subs[0].Auth.ShouldBe("a-new");
    }

    [Fact]
    public async Task SaveSubscriptionAsync_DifferentEndpoints_CoexistForSameUser()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1"), tenantId: null, TestContext.Current.CancellationToken);
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/2"), tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetSubscriptionsAsync_OtherUser_NotReturned()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1"), tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-2", null, TestContext.Current.CancellationToken);

        subs.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveSubscriptionAsync_DeletesByUserAndEndpoint()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1"), tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveSubscriptionAsync("user-1", "https://push/1", tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveSubscriptionAsync_OtherEndpoint_DoesNotDelete()
    {
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1"), tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveSubscriptionAsync("user-1", "https://push/other", tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RemoveSubscriptionAsync_AnotherUsersEndpoint_DoesNotDelete()
    {
        // Least privilege: knowing another user's endpoint must not allow deleting it.
        await _store.SaveSubscriptionAsync("user-1", Sub("https://push/1"), tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveSubscriptionAsync("user-2", "https://push/1", tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<WebPushSubscriptionInfo> subs = await _store
            .GetSubscriptionsAsync("user-1", null, TestContext.Current.CancellationToken);

        subs.ShouldHaveSingleItem();
    }
}
