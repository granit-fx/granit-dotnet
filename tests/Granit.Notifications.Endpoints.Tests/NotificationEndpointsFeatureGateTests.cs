using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Authorization;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Extensions;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

/// <summary>
/// Covers the <c>INotificationFeatureGate</c> branches of <c>GET /notifications/types</c>
/// — separate from <see cref="NotificationEndpointsTests"/> so the harness can register
/// a feature-gate substitute without affecting the "no gate registered" assertion that
/// already lives in the main test class.
/// </summary>
public sealed class NotificationEndpointsFeatureGateTests : IAsyncDisposable
{
    private const string Prefix = "/notifications";

    private readonly INotificationDefinitionStore _definitionStore = Substitute.For<INotificationDefinitionStore>();
    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly INotificationFeatureGate _featureGate = Substitute.For<INotificationFeatureGate>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;

    public NotificationEndpointsFeatureGateTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(GateTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, GateTestAuthHandler>(
                GateTestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, GateTestPolicyProvider>();

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero));

        builder.Services.AddSingleton(Substitute.For<IUserNotificationReader>());
        builder.Services.AddSingleton(Substitute.For<IUserNotificationWriter>());
        builder.Services.AddSingleton(Substitute.For<INotificationPreferenceReader>());
        builder.Services.AddSingleton(Substitute.For<INotificationPreferenceWriter>());
        builder.Services.AddSingleton(Substitute.For<INotificationSubscriptionReader>());
        builder.Services.AddSingleton(Substitute.For<INotificationSubscriptionWriter>());
        builder.Services.AddSingleton(_definitionStore);
        builder.Services.AddSingleton(_permissionChecker);
        builder.Services.AddSingleton(_featureGate);
        builder.Services.AddSingleton(currentTenant);
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<IGuidGenerator>(new GateSimpleGuidGenerator());

        _permissionChecker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        _app = builder.Build();
        _app.MapGranitNotifications();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = _app.GetTestClient();
        _authClient.DefaultRequestHeaders.Add(GateTestAuthHandler.UserHeader, "user-123");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Shows_RequiredFeature_definition_when_gate_returns_true()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("feature.enabled") { RequiredFeature = "Workflow.Enabled" },
        ]);
        _featureGate.IsFeatureEnabledAsync("Workflow.Enabled", Arg.Any<CancellationToken>())
            .Returns(true);

        List<NotificationTypeResponse> response = await _authClient.GetFromJsonAsync<List<NotificationTypeResponse>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["feature.enabled"]);
    }

    [Fact]
    public async Task Hides_RequiredFeature_definition_when_gate_returns_false()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("feature.disabled") { RequiredFeature = "Workflow.Enabled" },
            new NotificationDefinition("ungated.event"),
        ]);
        _featureGate.IsFeatureEnabledAsync("Workflow.Enabled", Arg.Any<CancellationToken>())
            .Returns(false);

        List<NotificationTypeResponse> response = await _authClient.GetFromJsonAsync<List<NotificationTypeResponse>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["ungated.event"]);
    }

    [Fact]
    public async Task Hides_RequiredFeature_definition_when_gate_throws()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("feature.broken") { RequiredFeature = "Workflow.Enabled" },
            new NotificationDefinition("ungated.event"),
        ]);
        _featureGate.IsFeatureEnabledAsync("Workflow.Enabled", Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("gate misbehaved"));

        List<NotificationTypeResponse> response = await _authClient.GetFromJsonAsync<List<NotificationTypeResponse>>(
            $"{Prefix}/types", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException();

        response.Select(d => d.Name).ShouldBe(["ungated.event"]);
    }

    [Fact]
    public async Task Propagates_cancellation_from_gate_to_caller()
    {
        _definitionStore.GetAll().Returns(
        [
            new NotificationDefinition("feature.gated") { RequiredFeature = "Workflow.Enabled" },
        ]);
        _featureGate.IsFeatureEnabledAsync("Workflow.Enabled", Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new OperationCanceledException());

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await _authClient.GetFromJsonAsync<List<NotificationTypeResponse>>(
                $"{Prefix}/types", cts.Token));
    }

    private sealed class GateTestPolicyProvider : IAuthorizationPolicyProvider
    {
        private static readonly AuthorizationPolicy s_policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(s_policy);
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(s_policy);
    }

    private sealed class GateTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string UserHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out Microsoft.Extensions.Primitives.StringValues userHeader))
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

    private sealed class GateSimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}
