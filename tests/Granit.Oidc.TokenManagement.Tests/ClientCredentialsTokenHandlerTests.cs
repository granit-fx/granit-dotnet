using System.Diagnostics.Metrics;
using System.Net;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Cache;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.DPoP;
using Granit.Oidc.TokenManagement.Handlers;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class ClientCredentialsTokenHandlerTests : IDisposable
{
    private const string ClientName = "svc-client";
    private const string Authority = "https://auth.example.com";
    private const string ResourceUri = "https://api.example.com/resource";

    private readonly ITokenEndpointService _tokenEndpoint = Substitute.For<ITokenEndpointService>();
    private readonly IClientCredentialsTokenCache _cache = Substitute.For<IClientCredentialsTokenCache>();
    private readonly IDPoPProofService _dpopProof = Substitute.For<IDPoPProofService>();
    private readonly IDPoPKeyStore _dpopKeyStore = Substitute.For<IDPoPKeyStore>();
    private readonly IOptionsMonitor<ClientCredentialsOptions> _optionsMonitor =
        Substitute.For<IOptionsMonitor<ClientCredentialsOptions>>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TokenManagementMetrics _metrics;

    public ClientCredentialsTokenHandlerTests()
    {
        _metrics = new TokenManagementMetrics(_meterFactory);
        _dpopKeyStore.GetOrCreateKey(ClientName).Returns("private-key-jwk");
    }

    public void Dispose() => _meterFactory.Dispose();

    // ──── Cached token attached as Bearer, no endpoint round-trip ────

    [Fact]
    public async Task SendAsync_CachedToken_AttachesBearerWithoutFetching()
    {
        _cache.GetTokenAsync(ClientName, Arg.Any<CancellationToken>()).Returns("cached-token");
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await SendThroughHandler(inner, Options());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        inner.LastRequest.Headers.Authorization.Parameter.ShouldBe("cached-token");
        await _tokenEndpoint.DidNotReceive().RequestTokenAsync(
            Arg.Any<string>(), Arg.Any<ClientCredentialsTokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(), Arg.Any<CancellationToken>());
    }

    // ──── Cache miss → acquires and caches token ────

    [Fact]
    public async Task SendAsync_CacheMiss_AcquiresAndCachesToken()
    {
        _cache.GetTokenAsync(ClientName, Arg.Any<CancellationToken>()).Returns((string?)null);
        _tokenEndpoint.RequestTokenAsync(
                Authority, Arg.Any<ClientCredentialsTokenRequest>(),
                Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
                Arg.Any<DPoPOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new TokenResponse { AccessToken = "fresh-token", ExpiresIn = 3600 });
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await SendThroughHandler(inner, Options());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("fresh-token");
        await _cache.Received(1).SetTokenAsync(
            ClientName, "fresh-token", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    // ──── Acquisition failure → request sent without Authorization ────

    [Fact]
    public async Task SendAsync_TokenAcquisitionFails_SendsWithoutAuthorization()
    {
        _cache.GetTokenAsync(ClientName, Arg.Any<CancellationToken>()).Returns((string?)null);
        _tokenEndpoint.RequestTokenAsync(
                Authority, Arg.Any<ClientCredentialsTokenRequest>(),
                Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
                Arg.Any<DPoPOptions?>(), Arg.Any<CancellationToken>())
            .Returns(TokenResponse.FromError("invalid_client", "bad secret"));
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        HttpResponseMessage response = await SendThroughHandler(inner, Options());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization.ShouldBeNull();
    }

    // ──── 401 → invalidate cache, re-acquire, retry once ────

    [Fact]
    public async Task SendAsync_Unauthorized_InvalidatesCacheAndRetries()
    {
        _cache.GetTokenAsync(ClientName, Arg.Any<CancellationToken>()).Returns("stale-token", "renewed-token");
        int calls = 0;
        RecordingHandler inner = new(_ =>
        {
            calls++;
            return new HttpResponseMessage(calls == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        });

        HttpResponseMessage response = await SendThroughHandler(inner, Options());

        calls.ShouldBe(2);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _cache.Received(1).RemoveTokenAsync(ClientName, Arg.Any<CancellationToken>());
        inner.LastRequest!.Headers.Authorization!.Parameter.ShouldBe("renewed-token");
    }

    // ──── DPoP path attaches DPoP scheme + proof header ────

    [Fact]
    public async Task SendAsync_DPoPEnabled_AttachesDPoPSchemeAndProof()
    {
        _cache.GetTokenAsync(ClientName, Arg.Any<CancellationToken>()).Returns("dpop-token");
        _dpopProof.CreateProof("private-key-jwk", "GET", ResourceUri, Arg.Any<string?>())
            .Returns("dpop-proof");
        RecordingHandler inner = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        ClientCredentialsOptions options = Options();
        options.UseDPoP = true;

        HttpResponseMessage response = await SendThroughHandler(inner, options);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("DPoP");
        inner.LastRequest.Headers.TryGetValues("DPoP", out IEnumerable<string>? proof).ShouldBeTrue();
        proof!.Single().ShouldBe("dpop-proof");
    }

    // ──── Helpers ────

    private ClientCredentialsOptions Options()
    {
        ClientCredentialsOptions options = new()
        {
            Authority = Authority,
            ClientId = "client-id",
            ClientSecret = "secret",
            Scope = "api",
        };
        _optionsMonitor.Get(ClientName).Returns(options);
        return options;
    }

    private async Task<HttpResponseMessage> SendThroughHandler(RecordingHandler inner, ClientCredentialsOptions options)
    {
        _optionsMonitor.Get(ClientName).Returns(options);
        ClientCredentialsTokenHandler handler = new(
            _tokenEndpoint,
            _cache,
            _dpopProof,
            _dpopKeyStore,
            _optionsMonitor,
            Microsoft.Extensions.Options.Options.Create(new TokenManagementOptions()),
            _clock,
            _metrics,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            ClientName = ClientName,
            InnerHandler = inner,
        };

        using HttpMessageInvoker invoker = new(handler);
        return await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, ResourceUri),
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
