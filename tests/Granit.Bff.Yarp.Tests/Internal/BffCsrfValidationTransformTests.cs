using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Granit.Bff.Yarp.Tests.Internal;

public sealed class BffCsrfValidationTransformTests : IDisposable
{
    private readonly IBffCsrfTokenGenerator _csrfGenerator = Substitute.For<IBffCsrfTokenGenerator>();
    private readonly ServiceProvider _sp;
    private readonly BffMetrics _metrics;
    private readonly BffCsrfValidationTransform _transform;

    private readonly GranitBffOptions _bffOptions = new()
    {
        Frontends =
        [
            new BffFrontendOptions { Name = "admin", PathPrefix = "/admin" },
        ],
    };

    public BffCsrfValidationTransformTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        System.Diagnostics.Metrics.IMeterFactory meterFactory = _sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        _metrics = new BffMetrics(meterFactory);

        _transform = new BffCsrfValidationTransform(
            _csrfGenerator,
            Microsoft.Extensions.Options.Options.Create(_bffOptions),
            _metrics,
            NullLogger<BffCsrfValidationTransform>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task ApplyAsync_NonMutatingGet_DoesNotValidateCsrf()
    {
        RequestTransformContext context = CreateTransformContext("GET", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
        context.HttpContext.Response.StatusCode.ShouldNotBe(403);
    }

    [Fact]
    public async Task ApplyAsync_MutatingPost_WithValidCsrf_DoesNotReject()
    {
        _csrfGenerator.Validate("session-1", "valid-token").Returns(true);

        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });
        context.HttpContext.Request.Headers["X-CSRF-Token"] = "valid-token";

        await _transform.ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldNotBe(403);
    }

    [Fact]
    public async Task ApplyAsync_MutatingPost_WithMissingCsrf_Returns403()
    {
        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });
        // No X-CSRF-Token header

        await _transform.ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task ApplyAsync_MutatingPost_WithInvalidCsrf_Returns403()
    {
        _csrfGenerator.Validate("session-1", "bad-token").Returns(false);

        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });
        context.HttpContext.Request.Headers["X-CSRF-Token"] = "bad-token";

        await _transform.ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task ApplyAsync_NoRequireAuthMetadata_SkipsValidation()
    {
        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>());

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
        context.HttpContext.Response.StatusCode.ShouldNotBe(403);
    }

    [Fact]
    public async Task ApplyAsync_RequireAuthFalse_SkipsValidation()
    {
        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "false",
        });

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ApplyAsync_NoSessionCookie_SkipsValidation()
    {
        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(new Dictionary<string, string>());

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ApplyAsync_UnknownFrontend_SkipsValidation()
    {
        _bffOptions.Frontends.Clear();
        _bffOptions.Frontends.Add(new BffFrontendOptions { Name = "admin" });
        _bffOptions.Frontends.Add(new BffFrontendOptions { Name = "patient" });

        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "unknown",
        });

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ApplyAsync_NoFrontendMetadata_SingleFrontend_FallsBackToFirst()
    {
        _csrfGenerator.Validate("session-1", "valid-token").Returns(true);

        RequestTransformContext context = CreateTransformContext("POST", new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            // No Granit.Bff.Frontend key — falls back to first if only 1 configured
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });
        context.HttpContext.Request.Headers["X-CSRF-Token"] = "valid-token";

        await _transform.ApplyAsync(context);

        _csrfGenerator.Received(1).Validate("session-1", "valid-token");
    }

    [Fact]
    public async Task ApplyAsync_NoProxyFeature_SkipsValidation()
    {
        DefaultHttpContext httpContext = new() { Request = { Method = "POST" } };
        // No IReverseProxyFeature set in features
        RequestTransformContext context = new()
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
        };

        await _transform.ApplyAsync(context);

        _csrfGenerator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task ApplyAsync_AllMutatingMethods_ValidateCsrf(string method)
    {
        _csrfGenerator.Validate("session-1", "valid-token").Returns(true);

        RequestTransformContext context = CreateTransformContext(method, new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "admin",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-admin"] = "session-1" });
        context.HttpContext.Request.Headers["X-CSRF-Token"] = "valid-token";

        await _transform.ApplyAsync(context);

        _csrfGenerator.Received(1).Validate("session-1", "valid-token");
    }

    private static RequestTransformContext CreateTransformContext(
        string method,
        Dictionary<string, string> metadata)
    {
        DefaultHttpContext httpContext = new() { Request = { Method = method } };

        RouteConfig routeConfig = new()
        {
            RouteId = "test-route",
            ClusterId = "test-cluster",
            Metadata = metadata.AsReadOnly(),
        };

        ClusterState clusterState = new("test-cluster");
        RouteModel routeModel = new(routeConfig, clusterState, HttpTransformer.Default);

        IReverseProxyFeature proxyFeature = Substitute.For<IReverseProxyFeature>();
        proxyFeature.Route.Returns(routeModel);
        httpContext.Features.Set(proxyFeature);

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
        };
    }

    private static IRequestCookieCollection CreateCookies(Dictionary<string, string> cookies)
    {
        IRequestCookieCollection cookieCollection = Substitute.For<IRequestCookieCollection>();
        cookieCollection[Arg.Any<string>()].Returns(callInfo =>
        {
            string key = callInfo.Arg<string>();
            return cookies.TryGetValue(key, out string? value) ? value : null;
        });
        cookieCollection.Count.Returns(cookies.Count);
        cookieCollection.Keys.Returns(cookies.Keys);
        cookieCollection.ContainsKey(Arg.Any<string>()).Returns(callInfo => cookies.ContainsKey(callInfo.Arg<string>()));
        return cookieCollection;
    }
}
