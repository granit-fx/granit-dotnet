using Granit.Bff.Internal;
using Granit.Bff.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Tests.Internal;

public sealed class DistributedCacheBffTokenStoreTests : IDisposable
{
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly IOptions<GranitBffOptions> _options;
    private readonly DistributedCacheBffTokenStore _store;

    public DistributedCacheBffTokenStoreTests()
    {
        GranitBffOptions bffOptions = new() { SessionDuration = TimeSpan.FromHours(8) };
        _options = Microsoft.Extensions.Options.Options.Create(bffOptions);

        _store = new DistributedCacheBffTokenStore(_cache, _options);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task StoreAsync_StoresInCache_WithCorrectKeyPattern()
    {
        BffTokenSet tokens = new("access-token", "refresh-token", "id-token", DateTimeOffset.UtcNow.AddHours(1));

        await _store.StoreAsync("admin", "session-123", tokens, TestContext.Current.CancellationToken);

        MaybeValue<BffTokenSet> maybe = await _cache.TryGetAsync<BffTokenSet>(
            "bff:session:admin:session-123",
            token: TestContext.Current.CancellationToken);
        maybe.HasValue.ShouldBeTrue();
        maybe.Value.AccessToken.ShouldBe("access-token");
        maybe.Value.RefreshToken.ShouldBe("refresh-token");
        maybe.Value.IdToken.ShouldBe("id-token");
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredTokens()
    {
        BffTokenSet tokens = new("access-token", "refresh-token", "id-token", DateTimeOffset.UtcNow.AddHours(1));
        await _store.StoreAsync("admin", "session-456", tokens, TestContext.Current.CancellationToken);

        BffTokenSet? result = await _store.GetAsync("admin", "session-456", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token");
        result.RefreshToken.ShouldBe("refresh-token");
        result.IdToken.ShouldBe("id-token");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForMissingKey()
    {
        BffTokenSet? result = await _store.GetAsync("admin", "missing", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromCache()
    {
        BffTokenSet tokens = new("at", null, null, DateTimeOffset.UtcNow.AddHours(1));
        await _store.StoreAsync("admin", "session-789", tokens, TestContext.Current.CancellationToken);

        await _store.RemoveAsync("admin", "session-789", TestContext.Current.CancellationToken);

        BffTokenSet? result = await _store.GetAsync("admin", "session-789", TestContext.Current.CancellationToken);
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task StoreAsync_NullOrEmptyFrontendName_ThrowsArgumentException(string? frontendName)
    {
        BffTokenSet tokens = new("at", null, null, DateTimeOffset.UtcNow);

        await Should.ThrowAsync<ArgumentException>(
            () => _store.StoreAsync(frontendName!, "sid", tokens, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task StoreAsync_NullOrEmptySessionId_ThrowsArgumentException(string? sessionId)
    {
        BffTokenSet tokens = new("at", null, null, DateTimeOffset.UtcNow);

        await Should.ThrowAsync<ArgumentException>(
            () => _store.StoreAsync("admin", sessionId!, tokens, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoreAsync_NullTokens_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _store.StoreAsync("admin", "sid", null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NullOrEmptyFrontendName_ThrowsArgumentException(string? frontendName)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _store.GetAsync(frontendName!, "sid", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_NullOrEmptySessionId_ThrowsArgumentException(string? sessionId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _store.GetAsync("admin", sessionId!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RemoveAsync_NullOrEmptyFrontendName_ThrowsArgumentException(string? frontendName)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _store.RemoveAsync(frontendName!, "sid", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RemoveAsync_NullOrEmptySessionId_ThrowsArgumentException(string? sessionId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _store.RemoveAsync("admin", sessionId!, TestContext.Current.CancellationToken));
    }
}
