using Granit.Authentication.OpenIddict.Internal;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Authentication.OpenIddict.Tests.Internal;

public sealed class RequireDPoPMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoBearerScheme_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_NoAuthorizationHeader_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_BearerWithoutDPoP_Returns401()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer eyJhbGciOiJSUzI1NiJ9.test";
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_BearerWithoutDPoP_SetsWwwAuthenticateHeader()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer eyJhbGciOiJSUzI1NiJ9.test";
        RequireDPoPMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers.WWWAuthenticate.ToString().ShouldBe("DPoP");
    }

    [Fact]
    public async Task InvokeAsync_DPoPScheme_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "DPoP eyJhbGciOiJFUzI1NiJ9.proof";
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_BearerWithDPoPHeader_CallsNext()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Bearer eyJhbGciOiJSUzI1NiJ9.test";
        context.Request.Headers.Append("DPoP", "eyJhbGciOiJFUzI1NiJ9.proof");
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_BearerCaseInsensitive_Returns401()
    {
        bool nextCalled = false;
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "bearer eyJhbGciOiJSUzI1NiJ9.test";
        RequireDPoPMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }
}
