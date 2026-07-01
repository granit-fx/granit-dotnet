using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.MultiTenancy;
using Granit.Notifications.WebPush.Endpoints.Dtos;
using Granit.Notifications.WebPush.Endpoints.Extensions;
using Granit.Notifications.WebPush.Endpoints.Validators;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for the browser Web Push subscription endpoints, mapped via the opt-in
/// <see cref="WebPushSubscriptionEndpointRouteBuilderExtensions.MapGranitWebPushSubscriptions"/>.
/// </summary>
public sealed class WebPushSubscriptionEndpointsTests : IAsyncDisposable
{
    private const string Route = "/notifications/web-push/subscriptions";
    private const string UserId = "user-123";

    private const string SampleEndpoint = "https://fcm.googleapis.com/fcm/send/abc-123";
    private const string SampleP256dh = "BFooBarP256dhKey";
    private const string SampleAuth = "AuthSecret123";

    private readonly IWebPushSubscriptionReader _reader = Substitute.For<IWebPushSubscriptionReader>();
    private readonly IWebPushSubscriptionWriter _writer = Substitute.For<IWebPushSubscriptionWriter>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public WebPushSubscriptionEndpointsTests()
    {
        _currentTenant.IsAvailable.Returns(false);
        _reader.GetSubscriptionsAsync(UserId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestPolicyProvider>();
        builder.Services.AddProblemDetails();

        builder.Services.AddSingleton(_reader);
        builder.Services.AddSingleton(_writer);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddScoped<IValidator<WebPushSubscriptionRegisterRequest>, WebPushSubscriptionRegisterRequestValidator>();
        builder.Services.AddScoped<IValidator<WebPushSubscriptionRemoveRequest>, WebPushSubscriptionRemoveRequestValidator>();

        _app = builder.Build();
        _app.MapGranitWebPushSubscriptions();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(UserId);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static object ValidRegisterBody() => new
    {
        endpoint = SampleEndpoint,
        expirationTime = (long?)null,
        keys = new { p256dh = SampleP256dh, auth = SampleAuth },
    };

    // ── POST (register) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewSubscription_Returns201()
    {
        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Route, ValidRegisterBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _writer.Received(1).SaveSubscriptionAsync(
            UserId,
            Arg.Is<WebPushSubscriptionInfo>(s =>
                s.Endpoint == SampleEndpoint &&
                s.P256dh == SampleP256dh &&
                s.Auth == SampleAuth &&
                s.ExpirationTime == null),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ExistingSubscription_Returns200()
    {
        _reader.GetSubscriptionsAsync(UserId, null, Arg.Any<CancellationToken>())
            .Returns([new WebPushSubscriptionInfo
            {
                Endpoint = SampleEndpoint,
                P256dh = SampleP256dh,
                Auth = SampleAuth,
            }]);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Route, ValidRegisterBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_PassesExpirationTime()
    {
        object body = new
        {
            endpoint = SampleEndpoint,
            expirationTime = 1_900_000_000_000L,
            keys = new { p256dh = SampleP256dh, auth = SampleAuth },
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Route, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _writer.Received(1).SaveSubscriptionAsync(
            UserId,
            Arg.Is<WebPushSubscriptionInfo>(s => s.ExpirationTime == 1_900_000_000_000L),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_Unauthenticated_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            Route, ValidRegisterBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", SampleP256dh, SampleAuth)]                 // empty endpoint
    [InlineData("not-a-url", SampleP256dh, SampleAuth)]        // non-HTTPS URI
    [InlineData(SampleEndpoint, "", SampleAuth)]               // empty p256dh
    [InlineData(SampleEndpoint, SampleP256dh, "")]             // empty auth
    public async Task Register_InvalidBody_Returns422(string endpoint, string p256dh, string auth)
    {
        object body = new
        {
            endpoint,
            expirationTime = (long?)null,
            keys = new { p256dh, auth },
        };

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Route, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await _writer.DidNotReceive().SaveSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<WebPushSubscriptionInfo>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _reader.GetSubscriptionsAsync(UserId, tenantId, Arg.Any<CancellationToken>()).Returns([]);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Route, ValidRegisterBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _writer.Received(1).SaveSubscriptionAsync(
            UserId, Arg.Any<WebPushSubscriptionInfo>(), tenantId, Arg.Any<CancellationToken>());
    }

    // ── DELETE (remove, body-bound) ──────────────────────────────────────────

    [Fact]
    public async Task Remove_Returns204_AndBindsEndpointFromBody()
    {
        // Proves minimal-API [FromBody] binding on DELETE: the endpoint travels in the JSON body,
        // not the route, and must reach the handler.
        using HttpRequestMessage request = new(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(new WebPushSubscriptionRemoveRequest { Endpoint = SampleEndpoint }),
        };

        HttpResponseMessage response = await _authClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).RemoveSubscriptionAsync(SampleEndpoint, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_UnknownEndpoint_Returns204()
    {
        // The seam is a no-op for an unknown endpoint (default substitute behaviour): still 204.
        using HttpRequestMessage request = new(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(new WebPushSubscriptionRemoveRequest { Endpoint = "https://push.example/unknown" }),
        };

        HttpResponseMessage response = await _authClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).RemoveSubscriptionAsync(
            "https://push.example/unknown", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_Unauthenticated_Returns401()
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(new WebPushSubscriptionRemoveRequest { Endpoint = SampleEndpoint }),
        };

        HttpResponseMessage response = await _anonClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Remove_InvalidBody_Returns422()
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(new WebPushSubscriptionRemoveRequest { Endpoint = "" }),
        };

        HttpResponseMessage response = await _authClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await _writer.DidNotReceive().RemoveSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        using HttpRequestMessage request = new(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(new WebPushSubscriptionRemoveRequest { Endpoint = SampleEndpoint }),
        };

        HttpResponseMessage response = await _authClient.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).RemoveSubscriptionAsync(SampleEndpoint, tenantId, Arg.Any<CancellationToken>());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
        Microsoft.Extensions.Logging.ILoggerFactory logger,
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
