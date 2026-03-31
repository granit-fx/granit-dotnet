using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.Discovery;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Services;
using Granit.Oidc.TokenManagement.Services.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class TokenEndpointServiceTests : IDisposable
{
    private const string Authority = "https://auth.example.com";
    private const string TokenEndpoint = "https://auth.example.com/connect/token";

    private readonly IDiscoveryDocumentService _discoveryService = Substitute.For<IDiscoveryDocumentService>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IDPoPProofService _dpopProofService = Substitute.For<IDPoPProofService>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TokenManagementMetrics _metrics;
    private readonly TokenEndpointService _sut;

    public TokenEndpointServiceTests()
    {
        _metrics = new TokenManagementMetrics(_meterFactory);
        _sut = new TokenEndpointService(
            _discoveryService,
            _httpClientFactory,
            _dpopProofService,
            _metrics,
            NullLogger<TokenEndpointService>.Instance);

        OidcDiscoveryDocument disco = new()
        {
            Issuer = Authority,
            AuthorizationEndpoint = $"{Authority}/connect/authorize",
            TokenEndpoint = TokenEndpoint,
        };

        _discoveryService.GetAsync(Authority, Arg.Any<CancellationToken>())
            .Returns(disco);
    }

    public void Dispose() => _meterFactory.Dispose();

    [Fact]
    public void TokenEndpointService_CanBeConstructed()
    {
        _sut.ShouldNotBeNull();
        _sut.ShouldBeAssignableTo<ITokenEndpointService>();
    }

    [Fact]
    public void DPoPOptions_Record_PropertiesWork()
    {
        DPoPOptions options = new("private-key-jwk", "server-nonce");

        options.PrivateKeyJwk.ShouldBe("private-key-jwk");
        options.Nonce.ShouldBe("server-nonce");
    }

    [Fact]
    public void DPoPOptions_WithExpression_CreatesNewInstance()
    {
        DPoPOptions original = new("key-1");
        DPoPOptions updated = original with { Nonce = "new-nonce" };

        updated.PrivateKeyJwk.ShouldBe("key-1");
        updated.Nonce.ShouldBe("new-nonce");
        original.Nonce.ShouldBeNull();
    }

    // ──── Argument validation ────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RequestTokenAsync_NullOrEmptyAuthority_Throws(string? authority) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.RequestTokenAsync(authority!, CreateClientCredentialsRequest()));

    [Fact]
    public async Task RequestTokenAsync_NullRequest_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RequestTokenAsync(Authority, null!));

    // ──── Successful token request ────

    [Fact]
    public async Task RequestTokenAsync_SuccessfulResponse_ReturnsTokenResponse()
    {
        SetupHttpClient(CreateSuccessResponse());

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.AccessToken.ShouldBe("test-access-token");
    }

    // ──── Error response ────

    [Fact]
    public async Task RequestTokenAsync_ErrorResponse_ReturnsErrorTokenResponse()
    {
        string errorJson = JsonSerializer.Serialize(new
        {
            error = "invalid_client",
            error_description = "Client authentication failed",
        });
        HttpResponseMessage response = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json"),
        };
        SetupHttpClient(response);

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Error.ShouldBe("invalid_client");
        result.Error.ErrorDescription.ShouldBe("Client authentication failed");
    }

    // ──── HTTP error (network failure) ────

    [Fact]
    public async Task RequestTokenAsync_HttpRequestException_ReturnsHttpError()
    {
        FakeHttpMessageHandler handler = new(_ => throw new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri(Authority) };
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(httpClient);

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Error.ShouldBe("http_error");
        result.Error.ErrorDescription!.ShouldContain("Connection refused");
    }

    // ──── Client authentication strategy applied ────

    [Fact]
    public async Task RequestTokenAsync_WithClientAuth_AppliesStrategy()
    {
        SetupHttpClient(CreateSuccessResponse());
        IClientAuthenticationStrategy clientAuth = Substitute.For<IClientAuthenticationStrategy>();

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), clientAuth, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        clientAuth.Received(1).Apply(
            Arg.Any<Dictionary<string, string>>(),
            "test-client",
            TokenEndpoint);
    }

    // ──── DPoP proof injection ────

    [Fact]
    public async Task RequestTokenAsync_WithDPoP_AttachesDPoPHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        FakeHttpMessageHandler handler = new(req =>
        {
            capturedRequest = req;
            return CreateSuccessResponse();
        });
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(httpClient);

        _dpopProofService.CreateProof("my-key-jwk", "POST", TokenEndpoint, "nonce-1")
            .Returns("dpop-proof-abc");

        DPoPOptions dpop = new("my-key-jwk", "nonce-1");

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), dpop: dpop, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Headers.TryGetValues("DPoP", out IEnumerable<string>? values).ShouldBeTrue();
        values!.Single().ShouldBe("dpop-proof-abc");
    }

    // ──── DPoP nonce retry ────

    [Fact]
    public async Task RequestTokenAsync_DPoPNonceRetry_RetriesWithNewNonce()
    {
        int callCount = 0;
        FakeHttpMessageHandler handler = new(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call: use_dpop_nonce error with new nonce in header
                string errorJson = JsonSerializer.Serialize(new { error = "use_dpop_nonce" });
                HttpResponseMessage errorResp = new(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json"),
                };
                errorResp.Headers.Add("DPoP-Nonce", "server-nonce-42");
                return errorResp;
            }

            // Second call: success
            return CreateSuccessResponse();
        });
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(httpClient);

        _dpopProofService.CreateProof(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns("dpop-proof");

        DPoPOptions dpop = new("key-jwk");

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), dpop: dpop, cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        callCount.ShouldBe(2);

        // Verify the second call used the server nonce
        _dpopProofService.Received(1).CreateProof("key-jwk", "POST", TokenEndpoint, "server-nonce-42");
    }

    // ──── DPoP nonce in success response ────

    [Fact]
    public async Task RequestTokenAsync_DPoPNonceInSuccessResponse_PropagatesNonce()
    {
        HttpResponseMessage response = CreateSuccessResponse();
        response.Headers.Add("DPoP-Nonce", "fresh-nonce");
        SetupHttpClient(response);

        TokenResponse result = await _sut.RequestTokenAsync(
            Authority, CreateClientCredentialsRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.DPoPNonce.ShouldBe("fresh-nonce");
    }

    // ──── Helpers ────

    private static ClientCredentialsTokenRequest CreateClientCredentialsRequest() =>
        new() { ClientId = "test-client", Scope = "api" };

    private static HttpResponseMessage CreateSuccessResponse()
    {
        string json = JsonSerializer.Serialize(new
        {
            access_token = "test-access-token",
            token_type = "Bearer",
            expires_in = 3600,
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private void SetupHttpClient(HttpResponseMessage response)
    {
        FakeHttpMessageHandler handler = new(_ => response);
        HttpClient httpClient = new(handler);
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(httpClient);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
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
