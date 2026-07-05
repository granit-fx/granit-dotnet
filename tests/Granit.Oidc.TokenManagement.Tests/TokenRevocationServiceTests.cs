using System.Diagnostics.Metrics;
using System.Net;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.Discovery;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Services;
using Granit.Oidc.TokenManagement.Services.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class TokenRevocationServiceTests : IDisposable
{
    private const string Authority = "https://auth.example.com";
    private const string RevocationEndpoint = "https://auth.example.com/connect/revocation";

    private readonly IDiscoveryDocumentService _discoveryService = Substitute.For<IDiscoveryDocumentService>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IDPoPProofService _dpopProofService = Substitute.For<IDPoPProofService>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TokenManagementMetrics _metrics;
    private readonly TokenRevocationService _sut;

    public TokenRevocationServiceTests()
    {
        _metrics = new TokenManagementMetrics(_meterFactory);
        _sut = new TokenRevocationService(
            _discoveryService,
            _httpClientFactory,
            _dpopProofService,
            _metrics,
            NullLogger<TokenRevocationService>.Instance);

        _discoveryService.GetAsync(Authority, Arg.Any<CancellationToken>())
            .Returns(CreateDisco(RevocationEndpoint));
    }

    public void Dispose() => _meterFactory.Dispose();

    [Fact]
    public void TokenRevocationService_ImplementsInterface() =>
        _sut.ShouldBeAssignableTo<ITokenRevocationService>();

    // ──── Argument validation ────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RevokeTokenAsync_NullOrEmptyAuthority_Throws(string? authority) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.RevokeTokenAsync(authority!, CreateRequest()));

    [Fact]
    public async Task RevokeTokenAsync_NullRequest_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.RevokeTokenAsync(Authority, null!));

    // ──── No revocation endpoint advertised ────

    [Fact]
    public async Task RevokeTokenAsync_NoRevocationEndpoint_ReturnsFalse()
    {
        _discoveryService.GetAsync(Authority, Arg.Any<CancellationToken>())
            .Returns(CreateDisco(revocationEndpoint: null));

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // ──── Successful revocation ────

    [Fact]
    public async Task RevokeTokenAsync_SuccessResponse_ReturnsTrue()
    {
        SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK));

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    // ──── Non-success status ────

    [Fact]
    public async Task RevokeTokenAsync_ErrorStatus_ReturnsFalse()
    {
        SetupHttpClient(new HttpResponseMessage(HttpStatusCode.BadRequest));

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // ──── Network failure ────

    [Fact]
    public async Task RevokeTokenAsync_HttpRequestException_ReturnsFalse()
    {
        FakeHttpMessageHandler handler = new(_ => throw new HttpRequestException("Connection refused"));
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(new HttpClient(handler));

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // ──── Client authentication strategy applied ────

    [Fact]
    public async Task RevokeTokenAsync_WithClientAuth_AppliesStrategy()
    {
        SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
        IClientAuthenticationStrategy clientAuth = Substitute.For<IClientAuthenticationStrategy>();

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), clientAuth, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        clientAuth.Received(1).Apply(
            Arg.Any<Dictionary<string, string>>(),
            "test-client",
            RevocationEndpoint);
    }

    // ──── DPoP proof injection ────

    [Fact]
    public async Task RevokeTokenAsync_WithDPoP_AttachesDPoPHeader()
    {
        HttpRequestMessage? captured = null;
        FakeHttpMessageHandler handler = new(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(new HttpClient(handler));
        _dpopProofService.CreateProof("key-jwk", "POST", RevocationEndpoint, "nonce-1")
            .Returns("dpop-proof-xyz");

        DPoPOptions dpop = new("key-jwk", "nonce-1");

        bool result = await _sut.RevokeTokenAsync(
            Authority, CreateRequest(), dpop: dpop, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.Headers.TryGetValues("DPoP", out IEnumerable<string>? values).ShouldBeTrue();
        values!.Single().ShouldBe("dpop-proof-xyz");
    }

    // ──── Helpers ────

    private static OidcDiscoveryDocument CreateDisco(string? revocationEndpoint) => new()
    {
        Issuer = Authority,
        AuthorizationEndpoint = $"{Authority}/connect/authorize",
        TokenEndpoint = $"{Authority}/connect/token",
        RevocationEndpoint = revocationEndpoint,
    };

    private static RevocationRequest CreateRequest() =>
        new() { Token = "token-to-revoke", ClientId = "test-client", TokenTypeHint = "access_token" };

    private void SetupHttpClient(HttpResponseMessage response)
    {
        FakeHttpMessageHandler handler = new(_ => response);
        _httpClientFactory.CreateClient("Granit.TokenManagement").Returns(new HttpClient(handler));
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
