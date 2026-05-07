using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

public sealed class InMemoryMobilePushTokenStoreTests
{
    private readonly InMemoryMobilePushTokenStore _store = new(new FakeHasher());

    [Fact]
    public async Task RegisterAsync_ThenGetTokensAsync_ReturnsToken()
    {
        await _store.RegisterAsync("user-1", "device-token-1", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldHaveSingleItem();
        result[0].DeviceToken.ShouldBe("device-token-1");
    }

    [Fact]
    public async Task RemoveAsync_RemovesToken()
    {
        await _store.RegisterAsync("user-1", "device-token-1", MobilePlatform.Ios, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveAsync("device-token-1", "user-1", null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_SameToken_Upserts()
    {
        await _store.RegisterAsync("user-1", "device-token-1", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RegisterAsync("user-1", "device-token-1", MobilePlatform.Ios, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldHaveSingleItem();
        result[0].Platform.ShouldBe(MobilePlatform.Ios);
    }

    [Fact]
    public async Task GetTokensAsync_DifferentUser_ReturnsEmpty()
    {
        await _store.RegisterAsync("user-1", "device-token-1", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> result = await _store.GetTokensAsync("user-2", null, TestContext.Current.CancellationToken);
        result.ShouldBeEmpty();
    }

    /// <summary>Identity hasher — for the in-memory store, uniqueness is the
    /// only contract that matters; collision-resistance is not.</summary>
    private sealed class FakeHasher : IMobilePushTokenHasher
    {
        public string? ComputeHash(string? deviceToken) =>
            string.IsNullOrEmpty(deviceToken) ? null : $"hash:{deviceToken}";
    }
}
