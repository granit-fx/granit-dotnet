// =============================================================================
// Tests - OnBehalfOfTokenHandler
// =============================================================================
// Replacement for the deleted AuthTokenPropagationHandler. Uses OAuth 2.0
// Token Exchange (RFC 8693) to swap the caller's inbound access token for
// an audience-scoped token (optionally DPoP-bound, RFC 9449). These tests
// exercise the security-critical fail-closed paths:
//   - No HttpContext (background job) → request sent unauthenticated
//   - Host not in AllowedHosts → request sent unauthenticated
//   - http:// target with RequireHttps → request sent unauthenticated
//   - IdP refusal → request sent unauthenticated (never falls back to naive
//     bearer propagation)
//   - Cache hit → no token endpoint call
//   - Happy path → exchanged token attached
// =============================================================================

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using Granit.Caching;
using Granit.MultiTenancy;
using Granit.Oidc.DPoP;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.DPoP;
using Granit.Oidc.TokenManagement.Handlers;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests.Handlers;

public sealed class OnBehalfOfTokenHandlerTests : IDisposable
{
    private const string ClientName = "test-obo";
    private const string AllowedHost = "downstream.internal";
    private const string InboundToken = "inbound-user-jwt";
    private const string ExchangedToken = "exchanged-audience-scoped-jwt";

    private readonly ITokenEndpointService _tokenEndpoint = Substitute.For<ITokenEndpointService>();
    private readonly IConditionalCache _cache = Substitute.For<IConditionalCache>();
    private readonly IDPoPProofService _dpop = Substitute.For<IDPoPProofService>();
    private readonly IDPoPKeyStore _dpopKeyStore = Substitute.For<IDPoPKeyStore>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IOptionsMonitor<OnBehalfOfOptions> _optionsMonitor = Substitute.For<IOptionsMonitor<OnBehalfOfOptions>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ServiceProvider _sp;
    private readonly TokenManagementMetrics _metrics;

    public OnBehalfOfTokenHandlerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new TokenManagementMetrics(meterFactory);

        _currentUser.UserId.Returns("user-42");
        _currentTenant.IsAvailable.Returns(false);
        _currentTenant.Id.Returns((Guid?)null);

        // Default options — happy path ready; individual tests tweak via ConfigureOptions.
        ConfigureOptions(new OnBehalfOfOptions
        {
            Authority = "https://idp.example.com",
            ClientId = "obo-client",
            ClientSecret = "obo-secret",
            Audience = "downstream-api",
            Scopes = ["api:read"],
            RequireDPoP = false, // keep tests simple; DPoP flows covered separately
            AllowedHosts = [AllowedHost],
            RequireHttps = true,
        });

        // Default: ambient HttpContext carries Bearer token
        SetInboundBearerToken(InboundToken);
    }

    public void Dispose() => _sp.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void ConfigureOptions(OnBehalfOfOptions options) =>
        _optionsMonitor.Get(Arg.Any<string>()).Returns(options);

    private void SetInboundBearerToken(string? token)
    {
        if (token is null)
        {
            _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
            return;
        }

        DefaultHttpContext ctx = new();
        ctx.Request.Headers.Authorization = $"Bearer {token}";
        _httpContextAccessor.HttpContext.Returns(ctx);
    }

    private (OnBehalfOfTokenHandler, CapturingHttpHandler) CreateHandler(
        HttpStatusCode downstreamStatus = HttpStatusCode.OK)
    {
        CapturingHttpHandler inner = new(_ => new HttpResponseMessage(downstreamStatus));
        OnBehalfOfTokenHandler handler = new(
            _tokenEndpoint,
            _cache,
            _dpop,
            _dpopKeyStore,
            _httpContextAccessor,
            _currentTenant,
            _currentUser,
            _optionsMonitor,
            _clock,
            _metrics,
            NullLogger<OnBehalfOfTokenHandler>.Instance)
        {
            ClientName = ClientName,
            InnerHandler = inner,
        };
        return (handler, inner);
    }

    private void StubCacheMiss() =>
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<string?>(null));

    private void StubSuccessfulTokenExchange() =>
        _tokenEndpoint.RequestTokenAsync(
                Arg.Any<string>(),
                Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
                Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
                Arg.Any<DPoPOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new TokenResponse
            {
                AccessToken = ExchangedToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                Scope = "api:read",
            });

    // -------------------------------------------------------------------------
    // Fail-closed: no HttpContext
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoHttpContext_RequestSentUnauthenticated()
    {
        SetInboundBearerToken(null); // background job → no HttpContext
        StubCacheMiss();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization.ShouldBeNull(
            "without an ambient HttpContext there is no user identity to exchange");

        await _tokenEndpoint.DidNotReceive().RequestTokenAsync(
            Arg.Any<string>(),
            Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Fail-closed: host allow-list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HostNotInAllowList_RequestSentUnauthenticated()
    {
        StubCacheMiss();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            "https://attacker.example.com/steal", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization.ShouldBeNull(
            "host must be allow-listed to receive an exchanged token");

        await _tokenEndpoint.DidNotReceive().RequestTokenAsync(
            Arg.Any<string>(),
            Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HttpTarget_WithRequireHttps_RequestSentUnauthenticated()
    {
        StubCacheMiss();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"http://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization.ShouldBeNull(
            "non-https target must not receive a token even if host is allow-listed");
    }

    // -------------------------------------------------------------------------
    // Fail-closed: IdP refuses the exchange
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IdPReturnsError_RequestSentUnauthenticated()
    {
        StubCacheMiss();

        _tokenEndpoint.RequestTokenAsync(
                Arg.Any<string>(),
                Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
                Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
                Arg.Any<DPoPOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(TokenResponse.FromError("invalid_grant", "subject_token not accepted"));

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization.ShouldBeNull(
            "IdP refusal must NOT fall back to naive bearer propagation");
    }

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ValidRequest_AttachesExchangedBearerToken()
    {
        StubCacheMiss();
        StubSuccessfulTokenExchange();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        AuthenticationHeaderValue? authHeader = inner.Captured!.Headers.Authorization;
        authHeader.ShouldNotBeNull();
        authHeader.Scheme.ShouldBe("Bearer");
        authHeader.Parameter.ShouldBe(ExchangedToken);
        authHeader.Parameter.ShouldNotBe(InboundToken, "downstream must see the AUDIENCE-SCOPED token, not the caller's");
    }

    [Fact]
    public async Task ValidRequest_CachesExchangedToken()
    {
        StubCacheMiss();
        StubSuccessfulTokenExchange();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler _) = CreateHandler();
        using HttpClient client = new(handler);

        using HttpResponseMessage response = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        await _cache.Received(1).SetIfAbsentAsync(
            Arg.Any<string>(),
            ExchangedToken,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CachedToken_DoesNotHitIdP()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<string?>(ExchangedToken));

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization!.Parameter.ShouldBe(ExchangedToken);

        await _tokenEndpoint.DidNotReceive().RequestTokenAsync(
            Arg.Any<string>(),
            Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Multi-tenant cache partitioning
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DifferentTenants_SameSubject_GetDifferentCacheKeys()
    {
        // Capture every cache key the handler writes, across both tenants.
        List<string> keys = [];
        _cache.SetIfAbsentAsync(
                Arg.Do<string>(k => keys.Add(k)),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        StubCacheMiss();
        StubSuccessfulTokenExchange();

        // Tenant A — same sub, same audience, same scopes
        var tenantA = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantA);

        (OnBehalfOfTokenHandler handlerA, CapturingHttpHandler _) = CreateHandler();
        using (HttpClient clientA = new(handlerA))
        {
            using HttpResponseMessage r1 = await clientA.GetAsync(
                $"https://{AllowedHost}/", TestContext.Current.CancellationToken);
        }

        // Tenant B — same sub, same audience, same scopes
        var tenantB = Guid.NewGuid();
        _currentTenant.Id.Returns(tenantB);

        (OnBehalfOfTokenHandler handlerB, CapturingHttpHandler _) = CreateHandler();
        using (HttpClient clientB = new(handlerB))
        {
            using HttpResponseMessage r2 = await clientB.GetAsync(
                $"https://{AllowedHost}/", TestContext.Current.CancellationToken);
        }

        keys.Count.ShouldBeGreaterThanOrEqualTo(2);
        keys[0].ShouldContain(tenantA.ToString());
        keys[^1].ShouldContain(tenantB.ToString());
        keys[0].ShouldNotBe(keys[^1],
            "cache keys for the same subject across tenants must differ — prevents cross-tenant token reuse");
    }

    // -------------------------------------------------------------------------
    // Fail-open: cache outage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CacheGetThrows_FallsBackToIdP_RequestStillAuthenticated()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns<Task<string?>>(_ => throw new InvalidOperationException("redis down"));
        StubSuccessfulTokenExchange();

        (OnBehalfOfTokenHandler handler, CapturingHttpHandler inner) = CreateHandler();
        using HttpClient client = new(handler);

        HttpResponseMessage _ = await client.GetAsync(
            $"https://{AllowedHost}/resource", TestContext.Current.CancellationToken);

        inner.Captured!.Headers.Authorization!.Parameter.ShouldBe(ExchangedToken,
            "cache outage must not block request authentication — fail-open on cache, fail-closed on IdP");
    }

    // -------------------------------------------------------------------------
    // Inner capture helper
    // -------------------------------------------------------------------------

    private sealed class CapturingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(respond(request));
        }
    }
}
