using System.Reflection;
using Granit.Bff.Endpoints.Extensions;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Bff.Endpoints.Tests.Extensions;

public sealed class BffEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapGranitBff_IsPublicExtensionMethod()
    {
        // Verify the extension method exists and is publicly accessible via reflection.
        // A full integration test requires a configured WebApplication with OIDC,
        // which is out of scope for unit tests. This validates the API surface.
        MethodInfo? method = typeof(BffEndpointRouteBuilderExtensions)
            .GetMethod(nameof(BffEndpointRouteBuilderExtensions.MapGranitBff));

        method.ShouldNotBeNull();
        method.IsStatic.ShouldBeTrue();
        method.IsPublic.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/bff/login")]
    [InlineData("/bff/callback")]
    [InlineData("/app/bff/login")]
    public async Task InvokeAsync_OnBffAuthPath_SetsClickjackingHeaders(string path)
    {
        bool nextCalled = false;
        BffSecurityHeadersMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        context.Response.Headers.ContentSecurityPolicy.ToString().ShouldBe("frame-ancestors 'none'");
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_OnNonBffPath_LeavesSecurityHeadersUntouched()
    {
        BffSecurityHeadersMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Path = "/api/orders";

        await middleware.InvokeAsync(context);

        context.Response.Headers.XFrameOptions.ToString().ShouldBeEmpty();
        context.Response.Headers.ContentSecurityPolicy.ToString().ShouldBeEmpty();
    }
}
