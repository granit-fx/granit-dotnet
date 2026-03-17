using System.Security.Claims;
using Granit.Http.OutputCaching.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class GdprCompliantOutputCachePolicyTests
{
    private readonly GdprCompliantOutputCachePolicy _sut = new();

    [Fact]
    public async Task CacheRequestAsync_WhenAuthenticated_DisablesCaching()
    {
        // Arrange
        OutputCacheContext context = CreateContext(authenticated: true);

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.ShouldBeFalse();
    }

    [Fact]
    public async Task CacheRequestAsync_WhenAnonymous_DoesNotDisableCaching()
    {
        // Arrange
        OutputCacheContext context = CreateContext(authenticated: false);
        bool originalValue = context.EnableOutputCaching;

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.ShouldBe(originalValue);
    }

    [Fact]
    public async Task ServeResponseAsync_WhenSetCookieHeader_DisallowsStorage()
    {
        // Arrange
        OutputCacheContext context = CreateContext(authenticated: false);
        context.HttpContext.Response.Headers["Set-Cookie"] = "session=abc";

        // Act
        await _sut.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.ShouldBeFalse();
    }

    [Fact]
    public async Task ServeResponseAsync_WhenNoSetCookieHeader_AllowsStorage()
    {
        // Arrange
        OutputCacheContext context = CreateContext(authenticated: false);
        bool originalValue = context.AllowCacheStorage;

        // Act
        await _sut.ServeResponseAsync(context, CancellationToken.None);

        // Assert
        context.AllowCacheStorage.ShouldBe(originalValue);
    }

    [Fact]
    public async Task ServeFromCacheAsync_IsNoOp()
    {
        OutputCacheContext context = CreateContext(authenticated: false);

        await _sut.ServeFromCacheAsync(context, CancellationToken.None);
    }

    private static OutputCacheContext CreateContext(bool authenticated)
    {
        DefaultHttpContext httpContext = new();

        if (authenticated)
        {
            ClaimsIdentity identity = new(
                [new Claim(ClaimTypes.Name, "testuser")],
                authenticationType: "TestScheme");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        return new OutputCacheContext { HttpContext = httpContext };
    }
}
