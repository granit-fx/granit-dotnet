using System.Text.Json;
using Granit.Bff.Internal;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.Internal;

public sealed class DistributedCacheBffTokenStoreTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IOptions<GranitBffOptions> _options;
    private readonly DistributedCacheBffTokenStore _store;

    public DistributedCacheBffTokenStoreTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        GranitBffOptions bffOptions = new() { SessionDuration = TimeSpan.FromHours(8) };
        _options = Microsoft.Extensions.Options.Options.Create(bffOptions);

        _store = new DistributedCacheBffTokenStore(_cache, _options, _clock);
    }

    [Fact]
    public async Task StoreAsync_StoresSerializedJsonInCache_WithCorrectKeyPattern()
    {
        BffTokenSet tokens = new("access-token", "refresh-token", "id-token", DateTimeOffset.UtcNow.AddHours(1));

        await _store.StoreAsync("admin", "session-123", tokens, TestContext.Current.CancellationToken);

        await _cache.Received(1).SetAsync(
            "bff:session:admin:session-123",
            Arg.Is<byte[]>(bytes => VerifySerializedTokens(bytes, tokens)),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpiration != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreAsync_SetsAbsoluteExpiration_BasedOnSessionDuration()
    {
        DateTimeOffset now = new(2026, 3, 22, 10, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);

        BffTokenSet tokens = new("at", null, null, now.AddHours(1));

        await _store.StoreAsync("patient", "sid-1", tokens, TestContext.Current.CancellationToken);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpiration == now.Add(TimeSpan.FromHours(8))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_ReturnsDeserializedTokens()
    {
        BffTokenSet tokens = new("access-token", "refresh-token", "id-token", DateTimeOffset.UtcNow.AddHours(1));
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions);

        _cache.GetAsync("bff:session:admin:session-456", Arg.Any<CancellationToken>())
            .Returns(serialized);

        BffTokenSet? result = await _store.GetAsync("admin", "session-456", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token");
        result.RefreshToken.ShouldBe("refresh-token");
        result.IdToken.ShouldBe("id-token");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForMissingKey()
    {
        _cache.GetAsync("bff:session:admin:missing", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        BffTokenSet? result = await _store.GetAsync("admin", "missing", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForEmptyBytes()
    {
        _cache.GetAsync("bff:session:admin:empty", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<byte>());

        BffTokenSet? result = await _store.GetAsync("admin", "empty", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromCache()
    {
        await _store.RemoveAsync("admin", "session-789", TestContext.Current.CancellationToken);

        await _cache.Received(1).RemoveAsync("bff:session:admin:session-789", Arg.Any<CancellationToken>());
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

    private static bool VerifySerializedTokens(byte[] bytes, BffTokenSet expected)
    {
        BffTokenSet? deserialized = JsonSerializer.Deserialize<BffTokenSet>(bytes, JsonOptions);
        return deserialized is not null
            && deserialized.AccessToken == expected.AccessToken
            && deserialized.RefreshToken == expected.RefreshToken
            && deserialized.IdToken == expected.IdToken;
    }
}
