using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Internal;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

public sealed class InMemoryMobilePushTokenStoreTests
{
    private readonly InMemoryMobilePushTokenStore _store = new();

    [Fact]
    public async Task RegisterAsync_ThenGetTokensAsync_ReturnsToken()
    {
        var token = new MobilePushTokenInfo { UserId = "user-1", DeviceToken = "device-token-1", Platform = MobilePlatform.Android };
        await _store.RegisterAsync(token, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushTokenInfo> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldHaveSingleItem();
        result[0].DeviceToken.ShouldBe("device-token-1");
    }

    [Fact]
    public async Task RemoveAsync_RemovesToken()
    {
        var token = new MobilePushTokenInfo { UserId = "user-1", DeviceToken = "device-token-1", Platform = MobilePlatform.Ios };
        await _store.RegisterAsync(token, TestContext.Current.CancellationToken);
        await _store.RemoveAsync("device-token-1", "user-1", null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushTokenInfo> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_SameToken_Upserts()
    {
        var v1 = new MobilePushTokenInfo { UserId = "user-1", DeviceToken = "device-token-1", Platform = MobilePlatform.Android };
        var v2 = new MobilePushTokenInfo { UserId = "user-1", DeviceToken = "device-token-1", Platform = MobilePlatform.Ios };
        await _store.RegisterAsync(v1, TestContext.Current.CancellationToken);
        await _store.RegisterAsync(v2, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushTokenInfo> result = await _store.GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);
        result.ShouldHaveSingleItem();
        result[0].Platform.ShouldBe(MobilePlatform.Ios);
    }

    [Fact]
    public async Task GetTokensAsync_DifferentUser_ReturnsEmpty()
    {
        var token = new MobilePushTokenInfo { UserId = "user-1", DeviceToken = "device-token-1", Platform = MobilePlatform.Android };
        await _store.RegisterAsync(token, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushTokenInfo> result = await _store.GetTokensAsync("user-2", null, TestContext.Current.CancellationToken);
        result.ShouldBeEmpty();
    }
}
