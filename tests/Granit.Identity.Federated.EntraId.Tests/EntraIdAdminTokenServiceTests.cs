using System.Net;
using Granit.Identity.Federated.EntraId.Internal;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdAdminTokenServiceTests : IDisposable
{
    private readonly EntraIdAdminOptions _options = new()
    {
        TenantId = "test-tenant-id",
        ClientId = "admin-service",
        ClientSecret = "secret",
        ServicePrincipalObjectId = "sp-object-id",
    };

    private readonly MockHttpMessageHandler _handler = new();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly EntraIdAdminTokenService _service;

    public EntraIdAdminTokenServiceTests()
    {
        _handler.ResponseBody = """{"access_token":"test-token","expires_in":300}""";
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        HttpClient client = new(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(client);

        _service = new EntraIdAdminTokenService(
            factory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            NullLogger<EntraIdAdminTokenService>.Instance);
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
    public async Task GetTokenAsync_RefreshesExpiredToken()
    {
        MockSequenceHttpMessageHandler sequenceHandler = new(
        [
            """{"access_token":"token-1","expires_in":1}""",
            """{"access_token":"token-2","expires_in":300}""",
        ]);
        HttpClient client = new(sequenceHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(client);

        using var service = new EntraIdAdminTokenService(
            factory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        string token1 = await service.GetTokenAsync(TestContext.Current.CancellationToken);
        token1.ShouldBe("token-1");
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

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetTokenAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Entra ID token endpoint returned an empty access token.");
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
        body.ShouldContain("scope=https");
        body.ShouldContain("graph.microsoft.com");
    }

    [Fact]
    public async Task GetTokenAsync_CallsCorrectTokenEndpoint()
    {
        await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("login.microsoftonline.com/test-tenant-id/oauth2/v2.0/token");
    }

    [Fact]
    public async Task GetTokenAsync_UsesCorrectHttpMethod()
    {
        await _service.GetTokenAsync(TestContext.Current.CancellationToken);

        _handler.Requests[0].Method.ShouldBe("POST");
    }

    public void Dispose() => _service.Dispose();
}
