using Granit.MultiTenancy;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.EntityFrameworkCore.Internal;
using Granit.Notifications.MobilePush.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies that the EF Core mobile push token store routes upsert / remove
/// queries through <see cref="IMobilePushTokenHasher"/> — the encrypted
/// <see cref="MobilePushToken.DeviceToken"/> column is non-deterministic
/// (AES-CBC random IV) and cannot serve equality lookups.
/// </summary>
public sealed class EfCoreMobilePushTokenStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreMobilePushTokenStore _store;

    public EfCoreMobilePushTokenStoreTests()
    {
        _store = new EfCoreMobilePushTokenStore(
            _factory,
            Substitute.For<ICurrentTenant>(),
            new IdentityHasher());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RegisterAsync_NewToken_PersistsHashAndPlaintext()
    {
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens = await _store
            .GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.ShouldHaveSingleItem();
        tokens[0].DeviceToken.ShouldBe("device-token-A");
        tokens[0].DeviceTokenHash.ShouldBe("hash:device-token-A");
        tokens[0].Platform.ShouldBe(MobilePlatform.Android);
    }

    [Fact]
    public async Task RegisterAsync_SameToken_UpsertsViaHashLookup()
    {
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Ios, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens = await _store
            .GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.ShouldHaveSingleItem();
        tokens[0].Platform.ShouldBe(MobilePlatform.Ios);
    }

    [Fact]
    public async Task RegisterAsync_DifferentTokens_CoexistForSameUser()
    {
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RegisterAsync("user-1", "device-token-B", MobilePlatform.Ios, tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens = await _store
            .GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAsync_DeletesByHash()
    {
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveAsync("device-token-A", "user-1", tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens = await _store
            .GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_OtherUserToken_DoesNotDelete()
    {
        await _store.RegisterAsync("user-1", "device-token-A", MobilePlatform.Android, tenantId: null, TestContext.Current.CancellationToken);
        await _store.RemoveAsync("device-token-A", "user-2", tenantId: null, TestContext.Current.CancellationToken);

        IReadOnlyList<MobilePushToken> tokens = await _store
            .GetTokensAsync("user-1", null, TestContext.Current.CancellationToken);

        tokens.ShouldHaveSingleItem();
    }

    /// <summary>Identity hasher — collisions are impossible in tests because
    /// each device token string is unique. Production uses HMAC-SHA256.</summary>
    private sealed class IdentityHasher : IMobilePushTokenHasher
    {
        public string? ComputeHash(string? deviceToken) =>
            string.IsNullOrEmpty(deviceToken) ? null : $"hash:{deviceToken}";
    }
}
