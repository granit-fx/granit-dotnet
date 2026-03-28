using System.Diagnostics.Metrics;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Bff.Yarp.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;
using MsOptions = Microsoft.Extensions.Options;

namespace Granit.Bff.Yarp.Tests.Internal;

public sealed class BffTokenInjectionTransformTests : IDisposable
{
    private readonly IBffTokenStore _tokenStore = Substitute.For<IBffTokenStore>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IDPoPProofService _dpopService = Substitute.For<IDPoPProofService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ServiceProvider _sp;
    private readonly BffMetrics _metrics;

    private readonly GranitBffOptions _bffOptions = new()
    {
        Authority = new Uri("https://auth.example.com"),
        UseSessionSlidingExpiration = false,
        RefreshGracePeriod = TimeSpan.FromSeconds(30),
        Frontends =
        [
            new BffFrontendOptions { Name = "main", ClientId = "client", ClientSecret = "s3cr3t" },
        ],
    };

    public BffTokenInjectionTransformTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        _metrics = new BffMetrics(meterFactory);
        _clock.Now.Returns(DateTimeOffset.UtcNow);
    }

    public void Dispose() => _sp.Dispose();

    private BffTokenInjectionTransform CreateTransform() => new(
        _tokenStore,
        MsOptions.Options.Create(_bffOptions),
        _httpClientFactory,
        _dpopService,
        _metrics,
        _clock,
        NullLogger<BffTokenInjectionTransform>.Instance);

    [Fact]
    public async Task ApplyAsync_NoProxyFeature_DoesNotReturn401()
    {
        DefaultHttpContext httpContext = new();
        RequestTransformContext context = new()
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(),
        };

        await CreateTransform().ApplyAsync(context);

        httpContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task ApplyAsync_RequireAuthFalse_DoesNotReturn401()
    {
        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "false",
        });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task ApplyAsync_RequireAuthTrue_NoSessionCookie_Returns401()
    {
        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ApplyAsync_StoreReturnsNull_Returns401()
    {
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns((BffTokenSet?)null);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ApplyAsync_ValidBearerToken_InjectsBearerAuthorizationHeader()
    {
        BffTokenSet tokens = new("access-token-xyz", null, null, DateTimeOffset.UtcNow.AddHours(1));
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(200);
        context.ProxyRequest.Headers.Authorization?.Scheme.ShouldBe("Bearer");
        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("access-token-xyz");
    }

    [Fact]
    public async Task ApplyAsync_DPoPToken_InjectsDPoPAuthorizationAndProofHeaders()
    {
        BffTokenSet tokens = new("dpop-access-token", null, null, DateTimeOffset.UtcNow.AddHours(1))
        {
            DPoPPrivateKeyJwk = """{"kty":"EC","crv":"P-256"}""",
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);
        _dpopService
            .CreateProof(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("dpop-proof-value");

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-granit-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Authorization?.Scheme.ShouldBe("DPoP");
        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("dpop-access-token");
        context.ProxyRequest.Headers.TryGetValues("DPoP", out IEnumerable<string>? proofValues).ShouldBeTrue();
        proofValues!.Single().ShouldBe("dpop-proof-value");
    }

    [Fact]
    public async Task ApplyAsync_UnknownFrontend_Returns401()
    {
        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "unknown",
        });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private static RequestTransformContext CreateTransformContext(Dictionary<string, string> metadata)
    {
        DefaultHttpContext httpContext = new() { Request = { Method = "GET" } };

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
        cookieCollection.ContainsKey(Arg.Any<string>()).Returns(callInfo =>
            cookies.ContainsKey(callInfo.Arg<string>()));
        return cookieCollection;
    }
}
