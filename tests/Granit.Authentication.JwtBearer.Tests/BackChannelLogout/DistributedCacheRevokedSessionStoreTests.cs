using Granit.Authentication.JwtBearer.BackChannelLogout;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authentication.JwtBearer.Tests.BackChannelLogout;

public sealed class DistributedCacheRevokedSessionStoreTests : IDisposable
{
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly DistributedCacheRevokedSessionStore _sut;

    public DistributedCacheRevokedSessionStoreTests()
    {
        ILogger<DistributedCacheRevokedSessionStore> logger =
            Substitute.For<ILogger<DistributedCacheRevokedSessionStore>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _sut = new DistributedCacheRevokedSessionStore(_cache, logger);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task RevokeSessionAsync_ValidSessionId_StoresInCache()
    {
        await _sut.RevokeSessionAsync("test-session-123", TimeSpan.FromMinutes(30),
            TestContext.Current.CancellationToken);

        MaybeValue<bool> maybe = await _cache.TryGetAsync<bool>(
            "granit:revoked-session:test-session-123",
            token: TestContext.Current.CancellationToken);
        maybe.HasValue.ShouldBeTrue();
    }

    [Fact]
    public async Task IsSessionRevokedAsync_RevokedSession_ReturnsTrue()
    {
        await _sut.RevokeSessionAsync("revoked-session", TimeSpan.FromMinutes(30),
            TestContext.Current.CancellationToken);

        bool result = await _sut.IsSessionRevokedAsync("revoked-session",
            TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsSessionRevokedAsync_UnknownSession_ReturnsFalse()
    {
        bool result = await _sut.IsSessionRevokedAsync("unknown-session",
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeSessionAsync_UsesConfiguredTtl_SetsCacheExpiration()
    {
        await _sut.RevokeSessionAsync("ttl-test", TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken);

        MaybeValue<bool> maybe = await _cache.TryGetAsync<bool>(
            "granit:revoked-session:ttl-test",
            token: TestContext.Current.CancellationToken);
        maybe.HasValue.ShouldBeTrue();
    }
}
