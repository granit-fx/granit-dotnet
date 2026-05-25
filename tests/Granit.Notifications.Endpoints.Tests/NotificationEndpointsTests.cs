using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Authorization;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Extensions;
using Granit.QueryEngine;
using Granit.Timing;
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

public sealed class NotificationEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/notifications";

    private readonly IUserNotificationReader _userNotificationReader = Substitute.For<IUserNotificationReader>();
    private readonly IUserNotificationWriter _userNotificationWriter = Substitute.For<IUserNotificationWriter>();
    private readonly INotificationPreferenceReader _preferenceReader = Substitute.For<INotificationPreferenceReader>();
    private readonly INotificationPreferenceWriter _preferenceWriter = Substitute.For<INotificationPreferenceWriter>();
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly INotificationSubscriptionWriter _subscriptionWriter = Substitute.For<INotificationSubscriptionWriter>();
    private readonly INotificationDefinitionStore _definitionStore = Substitute.For<INotificationDefinitionStore>();
    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public NotificationEndpointsTests()
    {
        _currentTenant.IsAvailable.Returns(false);
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, TestPolicyProvider>();

        builder.Services.AddSingleton(_userNotificationReader);
        builder.Services.AddSingleton(_userNotificationWriter);
        builder.Services.AddSingleton(_preferenceReader);
        builder.Services.AddSingleton(_preferenceWriter);
        builder.Services.AddSingleton(_subscriptionReader);
        builder.Services.AddSingleton(_subscriptionWriter);
        builder.Services.AddSingleton(_definitionStore);
        builder.Services.AddSingleton(_permissionChecker);
        builder.Services.AddSingleton(_currentTenant);
        builder.Services.AddSingleton(_clock);
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        _app = builder.Build();
        _app.MapGranitNotifications();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient("user-123");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET / (inbox) ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_Authenticated_Returns200()
    {
        _userNotificationReader.GetListAsync("user-123", null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>(Array.Empty<UserNotification>(), 0, HasMore: false));

        HttpResponseMessage response = await _authClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetNotifications_Unauthenticated_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(Prefix, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── GET /unread/count ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCount_Returns200WithCount()
    {
        _userNotificationReader.GetUnreadCountAsync("user-123", null, Arg.Any<CancellationToken>())
            .Returns(7);

        HttpResponseMessage response = await _authClient.GetAsync($"{Prefix}/unread/count", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        UnreadCountResponse? result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>(
            TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Count.ShouldBe(7);
    }

    [Fact]
    public async Task GetUnreadCount_Unauthenticated_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync($"{Prefix}/unread/count", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── POST /{id}/read ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsRead_Returns204()
    {
        var id = Guid.NewGuid();

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{id}/read", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _userNotificationWriter.Received(1).MarkAsReadAsync(id, "user-123", _clock.Now, Arg.Any<CancellationToken>());
    }

    // ── POST /read-all ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllAsRead_Returns204()
    {
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/read-all", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _userNotificationWriter.Received(1).MarkAllAsReadAsync(
            "user-123", null, _clock.Now, Arg.Any<CancellationToken>());
    }

    // ── GET /entity/{type}/{id} (activity feed) ────────────────────────────

    [Fact]
    public async Task GetEntityActivityFeed_Returns200()
    {
        _userNotificationReader.GetByEntityAsync("Patient", "42", null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>(Array.Empty<UserNotification>(), 0, HasMore: false));

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/entity/Patient/42", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── GET /preferences ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPreferences_Returns200()
    {
        _preferenceReader.GetListAsync("user-123", null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotificationPreference>());

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/preferences", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── PUT /preferences ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePreference_Returns204()
    {
        NotificationPreferenceUpdateRequest request = new()
        {
            NotificationTypeName = "Order.Shipped",
            ChannelName = "Email",
            IsEnabled = true,
        };

        HttpResponseMessage response = await _authClient.PutAsJsonAsync(
            $"{Prefix}/preferences", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _preferenceWriter.Received(1).SetAsync(
            Arg.Is<NotificationPreference>(p =>
                p.NotificationTypeName == "Order.Shipped" &&
                p.ChannelName == "Email" &&
                p.IsEnabled &&
                p.UserId == "user-123"),
            Arg.Any<CancellationToken>());
    }

    // ── GET /types ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationTypes_Returns200()
    {
        _definitionStore.GetAll().Returns(Array.Empty<NotificationDefinition>());

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/types", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetNotificationTypes_Hides_definitions_whose_RequiredPermission_user_lacks()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("ungated.event"),
            new NotificationDefinition("gated.event") { RequiredPermission = "Test.Resource.Read" },
        ]);
        _permissionChecker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        List<NotificationDefinition> response = await _authClient.GetFromJsonAsync<List<NotificationDefinition>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["ungated.event"]);
    }

    [Fact]
    public async Task GetNotificationTypes_Shows_definitions_whose_RequiredPermission_user_has()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("gated.event") { RequiredPermission = "Test.Resource.Read" },
        ]);
        _permissionChecker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(["Test.Resource.Read"]);

        List<NotificationDefinition> response = await _authClient.GetFromJsonAsync<List<NotificationDefinition>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["gated.event"]);
    }

    [Fact]
    public async Task GetNotificationTypes_Treats_unknown_permission_as_not_granted()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("gated.event") { RequiredPermission = "Missing.Permission" },
        ]);
        _permissionChecker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => throw new InvalidOperationException("not declared"));

        List<NotificationDefinition> response = await _authClient.GetFromJsonAsync<List<NotificationDefinition>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetNotificationTypes_With_no_feature_gate_registered_shows_RequiredFeature_definitions()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("feature.gated") { RequiredFeature = "Workflow.Enabled" },
        ]);
        _permissionChecker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        List<NotificationDefinition> response = await _authClient.GetFromJsonAsync<List<NotificationDefinition>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["feature.gated"]);
    }

    // ── GET /subscriptions ─────────────────────────────────────────────────

    [Fact]
    public async Task GetSubscriptions_Returns200()
    {
        _subscriptionReader.GetUserSubscriptionsAsync("user-123", null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotificationSubscription>());

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/subscriptions", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── POST /subscriptions/{typeName} ─────────────────────────────────────

    [Fact]
    public async Task Subscribe_Returns204()
    {
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/subscriptions/Order.Shipped", content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _subscriptionWriter.Received(1).SubscribeAsync(
            "user-123", "Order.Shipped", null, Arg.Any<CancellationToken>());
    }

    // ── DELETE /subscriptions/{typeName} ────────────────────────────────────

    [Fact]
    public async Task Unsubscribe_Returns204()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/subscriptions/Order.Shipped", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _subscriptionWriter.Received(1).UnsubscribeAsync(
            "user-123", "Order.Shipped", null, Arg.Any<CancellationToken>());
    }

    // ── POST /entity/{type}/{id}/follow ────────────────────────────────────

    [Fact]
    public async Task FollowEntity_Returns204()
    {
        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/entity/Patient/42/follow", content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _subscriptionWriter.Received(1).FollowEntityAsync(
            "user-123", "Patient", "42", null, Arg.Any<CancellationToken>());
    }

    // ── DELETE /entity/{type}/{id}/follow ───────────────────────────────────

    [Fact]
    public async Task UnfollowEntity_Returns204()
    {
        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/entity/Patient/42/follow", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _subscriptionWriter.Received(1).UnfollowEntityAsync(
            "user-123", "Patient", "42", null, Arg.Any<CancellationToken>());
    }

    // ── GET /entity/{type}/{id}/followers ───────────────────────────────────

    [Fact]
    public async Task GetEntityFollowers_Returns200()
    {
        _subscriptionReader.GetEntityFollowersAsync("Patient", "42", null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotificationSubscription>());

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/entity/Patient/42/followers", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Multi-tenancy ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_WithTenant_PassesTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _userNotificationReader.GetListAsync("user-123", tenantId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>(Array.Empty<UserNotification>(), 0, HasMore: false));

        HttpResponseMessage response = await _authClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _userNotificationReader.Received(1).GetListAsync(
            "user-123", tenantId, 1, 20, Arg.Any<CancellationToken>());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private HttpClient BuildClient(string userId)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, userId);
        return client;
    }

    // ── Fake policy provider (resolves any named policy as "require auth") ──

    internal sealed class TestPolicyProvider : IAuthorizationPolicyProvider
    {
        private static readonly AuthorizationPolicy s_policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(s_policy);
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(s_policy);
    }

    // ── Fake authentication handler ─────────────────────────────────────────

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
