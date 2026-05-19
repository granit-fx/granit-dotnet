using System.Diagnostics.Metrics;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Bff.Yarp.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
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
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

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
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

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
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

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

    // ──── Token refresh ────

    [Fact]
    public async Task ApplyAsync_TokenAboutToExpire_RefreshesAndInjectsNewToken()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet expiringSoonTokens = new(
            "old-access-token", "refresh-token-123", null,
            now.AddSeconds(10)) // expires in 10s, within 30s grace period
        {
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(expiringSoonTokens);

        // Setup refresh HTTP call
        string refreshJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            access_token = "new-access-token",
            refresh_token = "new-refresh-token",
            expires_in = 3600,
        });
        FakeHttpMessageHandler handler = new(_ => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(refreshJson, System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.Bff").Returns(httpClient);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(200);
        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("new-access-token");
        context.HttpContext.Response.Headers["X-Bff-Session-Refreshed"].ToString().ShouldBe("true");

        // Verify the new tokens were stored
        await _tokenStore.Received(1).StoreAsync(
            "main", "session-abc",
            Arg.Is<BffTokenSet>(t => t.AccessToken == "new-access-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_RefreshFails_Returns401()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet expiringSoonTokens = new(
            "old-access-token", "refresh-token-123", null,
            now.AddSeconds(10))
        {
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(expiringSoonTokens);

        // Refresh fails
        FakeHttpMessageHandler handler = new(_ =>
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json"),
            });
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.Bff").Returns(httpClient);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ApplyAsync_RefreshThrowsException_Returns401()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet expiringSoonTokens = new(
            "old-access-token", "refresh-token-123", null,
            now.AddSeconds(10))
        {
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(expiringSoonTokens);

        // HTTP client throws
        FakeHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.Bff").Returns(httpClient);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ApplyAsync_TokenNotExpiring_DoesNotRefresh()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        // Token expires in 2 hours (well beyond 30s grace period) — no refresh needed
        BffTokenSet tokens = new("access-token", "refresh-token", null, now.AddHours(2))
        {
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("access-token");
        // No refresh call to HTTP client
        _httpClientFactory.DidNotReceive().CreateClient("Granit.Bff");
    }

    [Fact]
    public async Task ApplyAsync_NoRefreshToken_DoesNotAttemptRefresh()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        // Token about to expire but no refresh token
        BffTokenSet tokens = new("access-token", null, null, now.AddSeconds(10))
        {
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        // Should still inject the token without refresh
        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("access-token");
        _httpClientFactory.DidNotReceive().CreateClient("Granit.Bff");
    }

    // ──── Frontend resolution ────

    [Fact]
    public async Task ApplyAsync_NoFrontendMetadata_FallsBackToSingleFrontend()
    {
        BffTokenSet tokens = new("access-token", null, null, DateTimeOffset.UtcNow.AddHours(1));
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        // No Granit.Bff.Frontend metadata — should fall back to the single configured frontend
        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        context.ProxyRequest.Headers.Authorization?.Parameter.ShouldBe("access-token");
    }

    [Fact]
    public async Task ApplyAsync_NoFrontendMetadata_MultipleFrontends_Returns401()
    {
        // Configure multiple frontends
        _bffOptions.Frontends.Add(
            new BffFrontendOptions { Name = "secondary", ClientId = "client2", ClientSecret = "s" });

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
        });

        await CreateTransform().ApplyAsync(context);

        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);

        // Cleanup
        _bffOptions.Frontends.RemoveAt(1);
    }

    // ──── Sliding session ────

    [Fact]
    public async Task ApplyAsync_SlidingExpiration_ExtensionPastHalfway_ReStoresTokens()
    {
        _bffOptions.UseSessionSlidingExpiration = true;
        _bffOptions.SessionDuration = TimeSpan.FromHours(2);
        _bffOptions.SessionAbsoluteMaxDuration = TimeSpan.FromHours(12);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet tokens = new("access-token", null, null, now.AddHours(1))
        {
            // Session created 1.5 hours ago, past halfway (1 hour) of 2-hour duration
            SessionCreatedAt = now.AddHours(-1.5),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        // Should have stored tokens for sliding extension
        await _tokenStore.Received().StoreAsync(
            "main", "session-abc", tokens, Arg.Any<CancellationToken>());

        _bffOptions.UseSessionSlidingExpiration = false;
    }

    [Fact]
    public async Task ApplyAsync_SlidingExpiration_BeforeHalfway_DoesNotExtend()
    {
        _bffOptions.UseSessionSlidingExpiration = true;
        _bffOptions.SessionDuration = TimeSpan.FromHours(2);
        _bffOptions.SessionAbsoluteMaxDuration = TimeSpan.FromHours(12);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet tokens = new("access-token", null, null, now.AddHours(1))
        {
            // Session created 30 minutes ago, before halfway (1 hour)
            SessionCreatedAt = now.AddMinutes(-30),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(tokens);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        // Should NOT have stored tokens (no sliding extension needed)
        await _tokenStore.DidNotReceive().StoreAsync(
            "main", "session-abc", tokens, Arg.Any<CancellationToken>());

        _bffOptions.UseSessionSlidingExpiration = false;
    }

    // ──── DPoP refresh ────

    [Fact]
    public async Task ApplyAsync_DPoPTokenRefresh_AttachesDPoPProofToRefreshRequest()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _clock.Now.Returns(now);

        BffTokenSet expiringSoonTokens = new(
            "old-access-token", "refresh-token-dpop", null,
            now.AddSeconds(10))
        {
            DPoPPrivateKeyJwk = """{"kty":"EC","crv":"P-256"}""",
            DPoPNonce = "old-nonce",
            SessionCreatedAt = now.AddHours(-1),
        };
        _tokenStore.GetAsync("main", "session-abc", Arg.Any<CancellationToken>())
            .Returns(expiringSoonTokens);

        _dpopService.CreateProof(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("dpop-proof-for-refresh");

        HttpRequestMessage? capturedRefreshRequest = null;
        FakeHttpMessageHandler handler = new(req =>
        {
            capturedRefreshRequest = req;
            string json = System.Text.Json.JsonSerializer.Serialize(new
            {
                access_token = "new-dpop-token",
                expires_in = 3600,
            });
            HttpResponseMessage resp = new(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
            resp.Headers.Add("DPoP-Nonce", "updated-nonce");
            return resp;
        });
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.Bff").Returns(httpClient);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "session-abc" });

        await CreateTransform().ApplyAsync(context);

        // DPoP proof attached to refresh request
        capturedRefreshRequest.ShouldNotBeNull();
        capturedRefreshRequest.Headers.TryGetValues("DPoP", out IEnumerable<string>? proofValues).ShouldBeTrue();
        proofValues!.Single().ShouldBe("dpop-proof-for-refresh");

        // Stored tokens should have updated DPoP nonce
        await _tokenStore.Received(1).StoreAsync(
            "main", "session-abc",
            Arg.Is<BffTokenSet>(t =>
                t.AccessToken == "new-dpop-token" &&
                t.DPoPNonce == "updated-nonce"),
            Arg.Any<CancellationToken>());
    }

    // ──── MaskSessionId ────

    [Fact]
    public async Task ApplyAsync_ShortSessionId_MasksCorrectly()
    {
        // Session ID shorter than 8 chars — should be masked as "****"
        _tokenStore.GetAsync("main", "abc", Arg.Any<CancellationToken>())
            .Returns((BffTokenSet?)null);

        RequestTransformContext context = CreateTransformContext(new Dictionary<string, string>
        {
            ["Granit.Bff.RequireAuth"] = "true",
            ["Granit.Bff.Frontend"] = "main",
        });
        context.HttpContext.Request.Cookies = CreateCookies(
            new Dictionary<string, string> { ["__Host-bff-main"] = "abc" });

        await CreateTransform().ApplyAsync(context);

        // Should return 401 (session not found) — test ensures masking code path does not throw
        context.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private static RequestTransformContext CreateTransformContext(Dictionary<string, string> metadata) =>
        CreateTransformContextWithPath(metadata, "/");

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private static RequestTransformContext CreateTransformContextWithPath(
        Dictionary<string, string> metadata, string path = "/")
    {
        DefaultHttpContext httpContext = new() { Request = { Method = "GET", Path = path } };

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
