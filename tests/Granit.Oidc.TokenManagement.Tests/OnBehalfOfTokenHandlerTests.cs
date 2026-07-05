using System.Diagnostics.Metrics;
using System.Net;
using Granit.Caching;
using Granit.MultiTenancy;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.DPoP;
using Granit.Oidc.TokenManagement.Handlers;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class OnBehalfOfTokenHandlerTests : IDisposable
{
    private const string ClientName = "downstream-api";
    private const string Authority = "https://auth.example.com";
    private const string AllowedHost = "api.example.com";
    private const string AllowedUri = "https://api.example.com/resource";

    private readonly ITokenEndpointService _tokenEndpoint = Substitute.For<ITokenEndpointService>();
    private readonly IConditionalCache _cache = Substitute.For<IConditionalCache>();
    private readonly IDPoPProofService _dpopProof = Substitute.For<IDPoPProofService>();
    private readonly IDPoPKeyStore _dpopKeyStore = Substitute.For<IDPoPKeyStore>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IOptionsMonitor<OnBehalfOfOptions> _optionsMonitor =
        Substitute.For<IOptionsMonitor<OnBehalfOfOptions>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TokenManagementMetrics _metrics;

    public OnBehalfOfTokenHandlerTests()
    {
        _metrics = new TokenManagementMetrics(_meterFactory);
        _dpopKeyStore.GetOrCreateKey(ClientName).Returns("obo-private-key");
        _currentUser.UserId.Returns("user-123");
        _currentTenant.IsAvailable.Returns(false);
        WithInboundToken("Bearer inbound-caller-token");
    }

    public void Dispose() => _meterFactory.Dispose();

    // ──── Target not allow-listed → unauthenticated passthrough ────

    [Fact]
    public async Task SendAsync_HostNotAllowed_SendsUnauthenticated()
    {
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, Options(), "https://evil.example.com/x");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
        await _tokenEndpoint.DidNotReceiveWithAnyArgs().RequestTokenAsync(
            "", null!, null, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_NonHttpsWhenRequireHttps_SendsUnauthenticated()
    {
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));
        OnBehalfOfOptions options = Options();
        options.AllowedHosts = ["api.example.com"];

        HttpResponseMessage response = await Send(inner, options, "http://api.example.com/x");

        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    // ──── No ambient HttpContext (background job) → unauthenticated ────

    [Fact]
    public async Task SendAsync_NoHttpContext_SendsUnauthenticated()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, Options(), AllowedUri);

        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_NoInboundAuthorizationHeader_SendsUnauthenticated()
    {
        WithInboundToken(null);
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, Options(), AllowedUri);

        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    // ──── Successful exchange → Bearer attached + write-through cache ────

    [Fact]
    public async Task SendAsync_SuccessfulExchange_AttachesBearerAndCaches()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        SetupExchange(new TokenResponse { AccessToken = "exchanged-token", ExpiresIn = 3600 });
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, NonDPoPOptions(), AllowedUri);

        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        inner.LastRequest.Headers.Authorization.Parameter.ShouldBe("exchanged-token");
        await _cache.Received(1).SetIfAbsentAsync(
            Arg.Any<string>(), "exchanged-token", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // ──── Cache hit → no exchange ────

    [Fact]
    public async Task SendAsync_CacheHit_UsesCachedTokenWithoutExchange()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("cached-token");
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, NonDPoPOptions(), AllowedUri);

        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("cached-token");
        await _tokenEndpoint.DidNotReceiveWithAnyArgs().RequestTokenAsync(
            "", null!, null, null, TestContext.Current.CancellationToken);
    }

    // ──── Exchange failure (fail-closed) → unauthenticated ────

    [Fact]
    public async Task SendAsync_ExchangeFails_SendsUnauthenticated()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        SetupExchange(TokenResponse.FromError("access_denied"));
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, NonDPoPOptions(), AllowedUri);

        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    // ──── Exchange throws → fail-closed, unauthenticated ────

    [Fact]
    public async Task SendAsync_ExchangeThrows_SendsUnauthenticated()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        _tokenEndpoint.RequestTokenAsync(
                Authority, Arg.Any<TokenExchangeTokenRequest>(),
                Arg.Any<IClientAuthenticationStrategy?>(), Arg.Any<DPoPOptions?>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new HttpRequestException("idp down"));
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, NonDPoPOptions(), AllowedUri);

        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    // ──── Cache read outage → fail-open, proceeds to exchange ────

    [Fact]
    public async Task SendAsync_CacheGetThrows_FailsOpenAndExchanges()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("redis down"));
        SetupExchange(new TokenResponse { AccessToken = "exchanged-token", ExpiresIn = 3600 });
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await Send(inner, NonDPoPOptions(), AllowedUri);

        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("exchanged-token");
    }

    // ──── DPoP: 401 nonce challenge triggers a single retry ────

    [Fact]
    public async Task SendAsync_DPoPNonceChallenge_RetriesOnce()
    {
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("dpop-token");
        _dpopProof.CreateProof("obo-private-key", "GET", AllowedUri, Arg.Any<string?>()).Returns("proof");

        int calls = 0;
        RecordingHandler inner = new(_ =>
        {
            calls++;
            if (calls == 1)
            {
                HttpResponseMessage challenge = new(HttpStatusCode.Unauthorized);
                challenge.Headers.Add("DPoP-Nonce", "server-nonce");
                return challenge;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        HttpResponseMessage response = await Send(inner, Options(), AllowedUri);

        calls.ShouldBe(2);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("DPoP");
    }

    // ──── Helpers ────

    private void WithInboundToken(string? authorizationHeader)
    {
        DefaultHttpContext ctx = new();
        if (authorizationHeader is not null)
        {
            ctx.Request.Headers.Authorization = authorizationHeader;
        }

        _httpContextAccessor.HttpContext.Returns(ctx);
    }

    private void SetupExchange(TokenResponse response) =>
        _tokenEndpoint.RequestTokenAsync(
                Authority, Arg.Any<TokenExchangeTokenRequest>(),
                Arg.Any<IClientAuthenticationStrategy?>(), Arg.Any<DPoPOptions?>(), Arg.Any<CancellationToken>())
            .Returns(response);

    private static OnBehalfOfOptions Options() => new()
    {
        Authority = Authority,
        ClientId = "client-id",
        ClientSecret = "secret",
        Audience = "https://api.example.com",
        AllowedHosts = [AllowedHost],
        RequireHttps = true,
        RequireDPoP = true,
    };

    private static OnBehalfOfOptions NonDPoPOptions()
    {
        OnBehalfOfOptions options = Options();
        options.RequireDPoP = false;
        return options;
    }

    private async Task<HttpResponseMessage> Send(RecordingHandler inner, OnBehalfOfOptions options, string uri)
    {
        _optionsMonitor.Get(ClientName).Returns(options);
        OnBehalfOfTokenHandler handler = new(
            _tokenEndpoint,
            _cache,
            _dpopProof,
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

        using HttpMessageInvoker invoker = new(handler);
        return await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, uri),
            TestContext.Current.CancellationToken);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(factory(request));
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
