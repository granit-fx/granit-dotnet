using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.MultiTenancy;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Endpoints;
using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class MobilePushTokenEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/api/notifications/mobile-push/tokens";

    private readonly IMobilePushTokenWriter _tokenWriter = Substitute.For<IMobilePushTokenWriter>();
    private readonly IMobilePushTokenReader _tokenReader = Substitute.For<IMobilePushTokenReader>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public MobilePushTokenEndpointsTests()
    {
        _currentTenant.IsAvailable.Returns(false);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestPolicyProvider>();

        builder.Services.AddSingleton(_tokenWriter);
        builder.Services.AddSingleton(_tokenReader);
        builder.Services.AddSingleton(_currentTenant);

        _app = builder.Build();
        _app.MapGranitMobilePushTokens();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient("user-456");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- POST / (register) -- new token returns 201 --

    [Fact]
    public async Task RegisterToken_NewToken_Returns201Created()
    {
        _tokenReader.GetTokensAsync("user-456", null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MobilePushToken>());

        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "fcm-token-abc",
            Platform = MobilePlatform.Android,
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _tokenWriter.Received(1).RegisterAsync(
            "user-456",
            "fcm-token-abc",
            MobilePlatform.Android,
            tenantId: null,
            Arg.Any<CancellationToken>());
    }

    // -- POST / (register) -- existing token returns 200 --

    [Fact]
    public async Task RegisterToken_ExistingToken_Returns200Ok()
    {
        var existing = MobilePushToken.Create(
            "user-456", "fcm-token-abc", deviceTokenHash: "hash-fcm-token-abc", MobilePlatform.Android);
        _tokenReader.GetTokensAsync("user-456", null, Arg.Any<CancellationToken>())
            .Returns([existing]);

        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "fcm-token-abc",
            Platform = MobilePlatform.Android,
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -- POST / (register) -- unauthenticated returns 401 --

    [Fact]
    public async Task RegisterToken_Unauthenticated_Returns401()
    {
        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "token",
            Platform = MobilePlatform.Ios,
        };

        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- DELETE /{deviceToken} --

    [Fact]
    public async Task RemoveToken_Returns204()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/my-device-token", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _tokenWriter.Received(1).RemoveAsync("my-device-token", "user-456", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveToken_Unauthenticated_Returns401()
    {
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/my-device-token", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- GET / (list tokens) --

    [Fact]
    public async Task GetTokens_ReturnsTokenList()
    {
        var tokens = new List<MobilePushToken>
        {
            MobilePushToken.Create("user-456", "token-1", deviceTokenHash: "hash-1", MobilePlatform.Android),
            MobilePushToken.Create("user-456", "token-2", deviceTokenHash: "hash-2", MobilePlatform.Ios),
        };
        _tokenReader.GetTokensAsync("user-456", null, Arg.Any<CancellationToken>())
            .Returns(tokens);

        HttpResponseMessage response = await _authClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<MobilePushTokenResponse>? result = await response.Content.ReadFromJsonAsync<List<MobilePushTokenResponse>>(
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Count.ShouldBe(2);
        result[0].DeviceToken.ShouldBe("token-1");
        result[0].Platform.ShouldBe(MobilePlatform.Android);
        result[1].DeviceToken.ShouldBe("token-2");
        result[1].Platform.ShouldBe(MobilePlatform.Ios);
    }

    [Fact]
    public async Task GetTokens_Unauthenticated_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -- Multi-tenancy --

    [Fact]
    public async Task RegisterToken_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _tokenReader.GetTokensAsync("user-456", tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MobilePushToken>());

        var request = new MobilePushTokenRegisterRequest
        {
            DeviceToken = "fcm-token-tenant",
            Platform = MobilePlatform.Android,
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _tokenWriter.Received(1).RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MobilePlatform>(), tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveToken_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/my-token", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _tokenWriter.Received(1).RemoveAsync("my-token", "user-456", tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTokens_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _tokenReader.GetTokensAsync("user-456", tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MobilePushToken>());

        HttpResponseMessage response = await _authClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _tokenReader.Received(1).GetTokensAsync("user-456", tenantId, Arg.Any<CancellationToken>());
    }

    // -- Helpers --

    private HttpClient BuildClient(string userId)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, userId);
        return client;
    }

    internal sealed class TestPolicyProvider : IAuthorizationPolicyProvider
    {
        private static readonly AuthorizationPolicy s_policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(s_policy);
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(s_policy);
    }

    internal sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues userHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string userId = userHeader.ToString();
            Claim[] claims =
            [
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, userId),
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
