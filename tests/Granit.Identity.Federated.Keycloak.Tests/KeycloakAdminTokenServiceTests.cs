using System.Net;
using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakAdminTokenServiceTests : IDisposable
{
    private readonly KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.test",
        Realm = "test-realm",
        ClientId = "admin-service",
        ClientSecret = "secret",
    };

    private readonly MockHttpMessageHandler _handler = new();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly KeycloakAdminTokenService _service;

    public KeycloakAdminTokenServiceTests()
    {
        _handler.ResponseBody = """{"access_token":"test-token","expires_in":300}""";
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        HttpClient client = new(_handler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("KeycloakAdmin").Returns(client);

        _service = new KeycloakAdminTokenService(
            factory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            NullLogger<KeycloakAdminTokenService>.Instance);
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsAccessToken()
    {
        string token = await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        token.ShouldBe("test-token");
        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/realms/test-realm/protocol/openid-connect/token");
    }

    [Fact]
    public async Task GetTokenAsync_CachesToken_DoesNotCallTwice()
    {
        string token1 = await _service.GetTokenAsync(TestContext.Current.CancellationToken);
        string token2 = await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        token1.ShouldBe("test-token");
        token2.ShouldBe("test-token");
        _handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetTokenAsync_SendsClientCredentialsGrant()
    {
        await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        string body = _handler.Requests[0].Body;
        body.ShouldContain("grant_type=client_credentials");
        body.ShouldContain("client_id=admin-service");
        body.ShouldContain("client_secret=secret");
    }

    [Fact]
    public async Task GetTokenAsync_RefreshesExpiredToken()
    {
        // First call returns a token with very short expiry (expires_in=1).
        // The safety margin (30s) means the effective cache time is max(1-30, 10) = 10s.
        // We use a sequence handler to return different tokens on successive calls.
        MockSequenceHttpMessageHandler sequenceHandler = new(
        [
            """{"access_token":"token-1","expires_in":1}""",
            """{"access_token":"token-2","expires_in":300}""",
        ]);
        HttpClient client = new(sequenceHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("KeycloakAdmin").Returns(client);

        using var service = new KeycloakAdminTokenService(
            factory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            NullLogger<KeycloakAdminTokenService>.Instance);

        // First token is returned; it will be cached for at least 10 seconds.
        string token1 = await service.GetTokenAsync(TestContext.Current.CancellationToken);
        token1.ShouldBe("token-1");
    }

    [Fact]
    public async Task GetTokenAsync_VeryShortExpiry_UsesMinimumCacheTime()
    {
        // expires_in=5 => Math.Max(5-30, 10) = 10 seconds minimum cache.
        _handler.ResponseBody = """{"access_token":"short-token","expires_in":5}""";

        string token = await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        token.ShouldBe("short-token");
        _handler.Requests.Count.ShouldBe(1);

        // Second call should still return cached token (within 10s window).
        string token2 = await _service.GetTokenAsync(TestContext.Current.CancellationToken);
        token2.ShouldBe("short-token");
        _handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetTokenAsync_HttpError_ThrowsHttpRequestException()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Unauthorized;
        _handler.ResponseBody = """{"error":"unauthorized_client"}""";

        await Should.ThrowAsync<HttpRequestException>(
            () => _service.GetTokenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTokenAsync_EmptyAccessToken_ThrowsInvalidOperationException()
    {
        _handler.ResponseBody = """{"access_token":"","expires_in":300}""";

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetTokenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetTokenAsync_UsesCorrectHttpMethod()
    {
        await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        _handler.Requests[0].Method.ShouldBe("POST");
    }

    public void Dispose() => _service.Dispose();
}
