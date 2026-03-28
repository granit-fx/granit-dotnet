using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

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
        context.Response.Headers.XXSSProtection.ToString().ShouldBe("0");
        context.Response.Headers["Permissions-Policy"].ToString()
            .ShouldBe("camera=(), microphone=(), geolocation=(), payment=(), " +
                       "accelerometer=(), gyroscope=(), magnetometer=(), usb=()");
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

    [Fact]
    public async Task Middleware_ReappliesHeaders_AfterResponseClear()
    {
        // Simulate ExceptionHandlerMiddleware clearing the response.
        // OnStarting callbacks survive Response.Clear() and re-apply headers.
        List<(Func<object, Task> Callback, object State)> onStartingCallbacks = [];
        CallbackCapturingResponseFeature responseFeature = new(onStartingCallbacks);
        FeatureCollection features = new();
        features.Set<IHttpResponseFeature>(responseFeature);
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));

        DefaultHttpContext context = new(features);
        GranitSecurityHeadersOptions options = new();
        SecurityHeadersMiddleware middleware = new(
            ctx =>
            {
                ctx.Response.Headers.Clear();
                return Task.CompletedTask;
            },
            Microsoft.Extensions.Options.Options.Create(options));

        await middleware.InvokeAsync(context);

        // Headers were cleared by the simulated exception handler
        context.Response.Headers.ContainsKey("X-Content-Type-Options").ShouldBeFalse();

        // Fire OnStarting as Kestrel would before sending the response
        foreach ((Func<object, Task> callback, object state) in onStartingCallbacks)
        {
            await callback(state);
        }

        context.Response.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");
        context.Response.Headers.XFrameOptions.ToString().ShouldBe("DENY");
        context.Response.Headers["Referrer-Policy"].ToString()
            .ShouldBe("strict-origin-when-cross-origin");
    }

    [Fact]
    public void ApplyHeaders_IsIdempotent()
    {
        HeaderDictionary headers = [];
        GranitSecurityHeadersOptions options = new();

        SecurityHeadersMiddleware.ApplyHeaders(headers, options);
        SecurityHeadersMiddleware.ApplyHeaders(headers, options);

        headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        headers["X-Frame-Options"].ToString().ShouldBe("DENY");
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

    private sealed class CallbackCapturingResponseFeature(
        List<(Func<object, Task>, object)> callbacks) : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state) =>
            callbacks.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
