using Granit.Http.Security.Internal;
using Granit.Http.Security.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Middleware_AddsDefaultSecurityHeaders()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        context.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        context.Response.Headers["Referrer-Policy"].ToString()
            .ShouldBe("strict-origin-when-cross-origin");
        context.Response.Headers["X-XSS-Protection"].ToString().ShouldBe("0");
        context.Response.Headers["Permissions-Policy"].ToString()
            .ShouldBe("camera=(), microphone=(), geolocation=(), payment=()");
        context.Response.Headers["Cross-Origin-Opener-Policy"].ToString()
            .ShouldBe("same-origin");
        context.Response.Headers["Cross-Origin-Resource-Policy"].ToString()
            .ShouldBe("same-origin");
    }

    [Fact]
    public async Task Middleware_OmitsContentSecurityPolicy_WhenNull()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Content-Security-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_AddsContentSecurityPolicy_WhenConfigured()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware(opts =>
            opts.ContentSecurityPolicy = "default-src 'self'");

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContentSecurityPolicy.ToString()
            .ShouldBe("default-src 'self'");
    }

    [Fact]
    public async Task Middleware_OmitsXFrameOptions_WhenNull()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware(opts =>
            opts.XFrameOptions = null);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_OmitsCrossOriginEmbedderPolicy_WhenNull()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("Cross-Origin-Embedder-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_AddsCrossOriginEmbedderPolicy_WhenConfigured()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware(opts =>
            opts.CrossOriginEmbedderPolicy = "require-corp");

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cross-Origin-Embedder-Policy"].ToString()
            .ShouldBe("require-corp");
    }

    [Fact]
    public async Task Middleware_DisablesAllHeaders_WhenConfiguredOff()
    {
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware(opts =>
        {
            opts.EnableContentTypeOptions = false;
            opts.XFrameOptions = null;
            opts.DisableXssProtection = false;
            opts.PermissionsPolicy = null;
            opts.ReferrerPolicy = "";
            opts.CrossOriginOpenerPolicy = "";
            opts.CrossOriginResourcePolicy = "";
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-Frame-Options").ShouldBeFalse();
        context.Response.Headers.ContainsKey("X-XSS-Protection").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Permissions-Policy").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Referrer-Policy").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Cross-Origin-Opener-Policy").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Cross-Origin-Resource-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Middleware_CallsNextDelegate()
    {
        bool nextCalled = false;
        GranitSecurityHeadersOptions options = new();
        SecurityHeadersMiddleware middleware = new(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Microsoft.Extensions.Options.Options.Create(options));
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
    }

    private static SecurityHeadersMiddleware CreateMiddleware(
        Action<GranitSecurityHeadersOptions>? configure = null)
    {
        GranitSecurityHeadersOptions options = new();
        configure?.Invoke(options);

        RequestDelegate next = _ => Task.CompletedTask;

        return new SecurityHeadersMiddleware(
            next,
            Microsoft.Extensions.Options.Options.Create(options));
    }
}
