using System.Diagnostics.Metrics;
using System.Net;
using Granit.Oidc.DPoP;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Cache;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Handlers;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options;

namespace Granit.Oidc.TokenManagement.Tests.Handlers;

public sealed class ClientCredentialsTokenHandlerTests : IDisposable
{
    private readonly ITokenEndpointService _tokenEndpointService = Substitute.For<ITokenEndpointService>();
    private readonly IClientCredentialsTokenCache _tokenCache = Substitute.For<IClientCredentialsTokenCache>();
    private readonly IDPoPProofService _dpopProofService = Substitute.For<IDPoPProofService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ServiceProvider _sp;
    private readonly TokenManagementMetrics _metrics;

    private readonly ClientCredentialsOptions _clientOptions = new()
    {
        Authority = "https://idp.example.com",
        ClientId = "test-client",
    };

    private readonly IOptionsMonitor<ClientCredentialsOptions> _optionsMonitor =
        Substitute.For<IOptionsMonitor<ClientCredentialsOptions>>();

    public ClientCredentialsTokenHandlerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        _metrics = new TokenManagementMetrics(meterFactory);
        _optionsMonitor.Get(Arg.Any<string>()).Returns(_clientOptions);
    }

    public void Dispose() => _sp.Dispose();

    private (ClientCredentialsTokenHandler, HttpRequestMessage captured) CreateHandlerWithCapture(
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        HttpRequestMessage? captured = null;
        var innerHandler = new DelegateHttpHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(statusCode);
        });

        var handler = new ClientCredentialsTokenHandler(
            _tokenEndpointService,
            _tokenCache,
            _dpopProofService,
            _optionsMonitor,
            MsOptions.Options.Create(new TokenManagementOptions()),
            _clock,
            _metrics,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            ClientName = "test-client",
            InnerHandler = innerHandler,
        };

        return (handler, captured!);
    }

    [Fact]
    public async Task SendAsync_CachedToken_DoesNotCallTokenEndpoint()
    {
        _tokenCache.GetTokenAsync("test-client", Arg.Any<CancellationToken>())
            .Returns("cached-token");

        (ClientCredentialsTokenHandler? handler, HttpRequestMessage _) = CreateHandlerWithCapture();
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.example.com/data", TestContext.Current.CancellationToken);

        await _tokenEndpointService.DidNotReceive().RequestTokenAsync(
            Arg.Any<string>(),
            Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_CachedToken_InjectsBearerHeader()
    {
        _tokenCache.GetTokenAsync("test-client", Arg.Any<CancellationToken>())
            .Returns("cached-bearer-token");

        HttpRequestMessage? captured = null;
        var innerHandler = new DelegateHttpHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new ClientCredentialsTokenHandler(
            _tokenEndpointService,
            _tokenCache,
            _dpopProofService,
            _optionsMonitor,
            MsOptions.Options.Create(new TokenManagementOptions()),
            _clock,
            _metrics,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            ClientName = "test-client",
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        await client.GetAsync("https://api.example.com/data", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.Authorization?.Scheme.ShouldBe("Bearer");
        captured.Headers.Authorization?.Parameter.ShouldBe("cached-bearer-token");
    }

    [Fact]
    public async Task SendAsync_NoCachedToken_CallsTokenEndpointAndInjectsToken()
    {
        _tokenCache.GetTokenAsync("test-client", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var tokenResponse = TokenResponse.FromJson(
            System.Text.Json.JsonDocument.Parse(
                """{"access_token":"fresh-token","expires_in":3600}""").RootElement,
            null);

        _tokenEndpointService.RequestTokenAsync(
            Arg.Any<string>(),
            Arg.Any<Granit.Oidc.Requests.TokenRequest>(),
            Arg.Any<Granit.Oidc.ClientAuthentication.IClientAuthenticationStrategy?>(),
            Arg.Any<DPoPOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(tokenResponse);

        HttpRequestMessage? captured = null;
        var innerHandler = new DelegateHttpHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new ClientCredentialsTokenHandler(
            _tokenEndpointService,
            _tokenCache,
            _dpopProofService,
            _optionsMonitor,
            MsOptions.Options.Create(new TokenManagementOptions()),
            _clock,
            _metrics,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            ClientName = "test-client",
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        await client.GetAsync("https://api.example.com/data", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Headers.Authorization?.Parameter.ShouldBe("fresh-token");
    }

    [Fact]
    public async Task SendAsync_On401_RemovesCachedTokenAndRetries()
    {
        _tokenCache.GetTokenAsync("test-client", Arg.Any<CancellationToken>())
            .Returns("stale-token", "refreshed-token");

        int callCount = 0;
        var innerHandler = new DelegateHttpHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new ClientCredentialsTokenHandler(
            _tokenEndpointService,
            _tokenCache,
            _dpopProofService,
            _optionsMonitor,
            MsOptions.Options.Create(new TokenManagementOptions()),
            _clock,
            _metrics,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            ClientName = "test-client",
            InnerHandler = innerHandler,
        };

        using var client = new HttpClient(handler);
        HttpResponseMessage response = await client.GetAsync("https://api.example.com/data", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _tokenCache.Received(1).RemoveTokenAsync("test-client", Arg.Any<CancellationToken>());
        callCount.ShouldBe(2);
    }

    private sealed class DelegateHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
