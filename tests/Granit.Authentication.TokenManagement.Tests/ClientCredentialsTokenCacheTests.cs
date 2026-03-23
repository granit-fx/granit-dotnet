using Granit.Authentication.TokenManagement.Cache.Internal;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Authentication.TokenManagement.Tests;

public sealed class ClientCredentialsTokenCacheTests
{
    private readonly ClientCredentialsTokenCache _sut;

    public ClientCredentialsTokenCacheTests()
    {
        MemoryDistributedCache memoryCache = new(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        _sut = new ClientCredentialsTokenCache(memoryCache, NullLogger<ClientCredentialsTokenCache>.Instance);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsCachedToken()
    {
        const string clientName = "test-client";
        const string accessToken = "cached-access-token-xyz";
        CancellationToken ct = TestContext.Current.CancellationToken;
        await _sut.SetTokenAsync(clientName, accessToken, TimeSpan.FromMinutes(5), ct);

        string? result = await _sut.GetTokenAsync(clientName, ct);

        result.ShouldBe(accessToken);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNullWhenMissing()
    {
        string? result = await _sut.GetTokenAsync("non-existent-client", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveTokenAsync_InvalidatesCache()
    {
        const string clientName = "client-to-remove";
        CancellationToken ct = TestContext.Current.CancellationToken;
        await _sut.SetTokenAsync(clientName, "token-value", TimeSpan.FromMinutes(5), ct);

        await _sut.RemoveTokenAsync(clientName, ct);

        string? result = await _sut.GetTokenAsync(clientName, ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetTokenAsync_OverwritesExistingToken()
    {
        const string clientName = "overwrite-client";
        CancellationToken ct = TestContext.Current.CancellationToken;
        await _sut.SetTokenAsync(clientName, "old-token", TimeSpan.FromMinutes(5), ct);
        await _sut.SetTokenAsync(clientName, "new-token", TimeSpan.FromMinutes(5), ct);

        string? result = await _sut.GetTokenAsync(clientName, ct);
        result.ShouldBe("new-token");
    }

    [Fact]
    public async Task GetTokenAsync_DifferentClients_ReturnCorrectTokens()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await _sut.SetTokenAsync("client-a", "token-a", TimeSpan.FromMinutes(5), ct);
        await _sut.SetTokenAsync("client-b", "token-b", TimeSpan.FromMinutes(5), ct);

        string? tokenA = await _sut.GetTokenAsync("client-a", ct);
        string? tokenB = await _sut.GetTokenAsync("client-b", ct);

        tokenA.ShouldBe("token-a");
        tokenB.ShouldBe("token-b");
    }
}
