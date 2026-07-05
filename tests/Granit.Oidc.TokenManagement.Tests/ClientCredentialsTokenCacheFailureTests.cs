using Granit.Oidc.TokenManagement.Cache.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable CA2012 // NSubstitute setup pattern — the faulted ValueTask is configured for replay, not consumed directly

namespace Granit.Oidc.TokenManagement.Tests;

/// <summary>
/// Verifies fail-open behaviour (cache outages must not fail the outbound request) and
/// argument validation for <see cref="ClientCredentialsTokenCache"/>.
/// </summary>
public sealed class ClientCredentialsTokenCacheFailureTests
{
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly ClientCredentialsTokenCache _sut;

    public ClientCredentialsTokenCacheFailureTests() =>
        _sut = new ClientCredentialsTokenCache(_cache, NullLogger<ClientCredentialsTokenCache>.Instance);

    // ──── Fail-open on cache outage ────

    [Fact]
    public async Task GetTokenAsync_CacheThrows_ReturnsNull()
    {
        _cache.TryGetAsync<string?>(Arg.Any<string>(), Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<MaybeValue<string?>>(new InvalidOperationException("cache down")));

        string? result = await _sut.GetTokenAsync("client", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetTokenAsync_CacheThrows_DoesNotThrow()
    {
        _cache.SetAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("cache down")));

        await Should.NotThrowAsync(() =>
            _sut.SetTokenAsync("client", "token", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveTokenAsync_CacheThrows_DoesNotThrow()
    {
        _cache.RemoveAsync(Arg.Any<string>(), Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("cache down")));

        await Should.NotThrowAsync(() =>
            _sut.RemoveTokenAsync("client", TestContext.Current.CancellationToken));
    }

    // ──── Cancellation propagates (not swallowed by fail-open) ────

    [Fact]
    public async Task GetTokenAsync_Cancelled_Propagates()
    {
        _cache.TryGetAsync<string?>(Arg.Any<string>(), Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<MaybeValue<string?>>(new OperationCanceledException()));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            _sut.GetTokenAsync("client", TestContext.Current.CancellationToken));
    }

    // ──── Argument validation ────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetTokenAsync_NullOrEmptyClient_Throws(string? clientName) =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.GetTokenAsync(clientName!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SetTokenAsync_NullOrEmptyClient_Throws(string? clientName) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.SetTokenAsync(clientName!, "token", TimeSpan.FromMinutes(1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SetTokenAsync_NullOrEmptyToken_Throws(string? accessToken) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.SetTokenAsync("client", accessToken!, TimeSpan.FromMinutes(1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RemoveTokenAsync_NullOrEmptyClient_Throws(string? clientName) =>
        await Should.ThrowAsync<ArgumentException>(() => _sut.RemoveTokenAsync(clientName!));
}

#pragma warning restore CA2012
