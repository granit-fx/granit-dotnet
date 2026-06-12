using System.Security.Claims;
using Granit.Http.OutputCaching.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class PrivateResponseOutputCachePolicyTests
{
    private readonly PrivateResponseOutputCachePolicy _sut = new();

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
    public async Task CacheRequestAsync_WhenAnonymousWithoutCredentials_DoesNotDisableCaching()
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
    public async Task CacheRequestAsync_WhenAuthorizationHeaderPresent_DisablesCaching()
    {
        // Arrange — credential carried on the request even though User is not populated
        // (the output-cache middleware commonly runs before authentication).
        OutputCacheContext context = CreateContext(authenticated: false);
        context.HttpContext.Request.Headers.Authorization = "Bearer token";

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.ShouldBeFalse();
    }

    [Fact]
    public async Task CacheRequestAsync_WhenCookieHeaderPresent_DisablesCaching()
    {
        // Arrange — cookie/session schemes (e.g. the BFF session cookie) authenticate inside
        // the endpoint and never set User, so a shared cache must not store their responses.
        OutputCacheContext context = CreateContext(authenticated: false);
        context.HttpContext.Request.Headers.Cookie = ".bff-host=abc";

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.EnableOutputCaching.ShouldBeFalse();
    }

    [Fact]
    public async Task ServeResponseAsync_WhenSetCookieHeader_DisallowsStorage()
    {
        // Arrange
        OutputCacheContext context = CreateContext(authenticated: false);
        context.HttpContext.Response.Headers.SetCookie = "session=abc";

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
        bool originalCaching = context.EnableOutputCaching;
        bool originalStorage = context.AllowCacheStorage;

        await _sut.ServeFromCacheAsync(context, CancellationToken.None);

        context.EnableOutputCaching.ShouldBe(originalCaching);
        context.AllowCacheStorage.ShouldBe(originalStorage);
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
