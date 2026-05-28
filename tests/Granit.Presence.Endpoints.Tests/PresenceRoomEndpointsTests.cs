using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.MultiTenancy;
using Granit.Presence.Abstractions;
using Granit.Presence.Endpoints.Dtos;
using Granit.Presence.Endpoints.Endpoints;
using Granit.Presence.Endpoints.Permissions;
using Granit.Presence.Endpoints.Validators;
using Granit.RateLimiting;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Options;
using Granit.Testing.Endpoints;
using Granit.Users;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for the resource-room endpoints. Drives a real <see cref="GranitEndpointTestHost"/>
/// with a custom authentication handler so the caller's Guid sub-claim flows through to
/// <c>PresenceCallerContext</c>.
/// </summary>
public sealed class PresenceRoomEndpointsTests
{
    private const string SchemeName = "GuidTest";
    private const string UserIdHeader = "X-Test-UserId";

    private static readonly ResourceRef SampleResource = new("document", "abc-123");

    private static async Task<GranitEndpointTestHost> StartAsync(
        IResourcePresenceTracker? tracker = null,
        IResourcePresenceVisibilityPolicy? visibilityPolicy = null) =>
        await GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                // Replace the default test auth scheme with one that mints a configurable Guid sub claim.
                services
                    .AddAuthentication(SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, GuidSubAuthHandler>(SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = SchemeName;
                    o.DefaultAuthenticateScheme = SchemeName;
                    o.DefaultChallengeScheme = SchemeName;
                });

                services.AddAuthorizationBuilder()
                    .AddPolicy(PresencePermissions.Rooms.Read,
                        p => p.RequireAuthenticatedUser())
                    .AddPolicy(PresencePermissions.Rooms.Join,
                        p => p.RequireAuthenticatedUser());

                services.AddSingleton(tracker ?? Substitute.For<IResourcePresenceTracker>());
                services.AddSingleton(visibilityPolicy ?? new AllowAllResourcePresenceVisibilityPolicyForTests());
                services.AddScoped<IValidator<HeartbeatRoomRequest>, HeartbeatRoomRequestValidator>();
                services.AddProblemDetails();
                services.AddMetrics();

                // Rate-limiting stack: pre-wire just enough for the `RequireGranitRateLimiting`
                // endpoint filter to short-circuit cheaply (Enabled = false).
                services.AddSingleton<ICurrentTenant, NullTenantContext>();
                services.AddSingleton<ICurrentUserService, SystemCurrentUserService>();
                services.Configure<GranitRateLimitingOptions>(o => o.Enabled = false);
                services.AddSingleton<IRateLimitCounterStore>(Substitute.For<IRateLimitCounterStore>());
                services.AddSingleton<IRateLimitQuotaProvider>(Substitute.For<IRateLimitQuotaProvider>());
                services.AddSingleton<RateLimitingMetrics>();
                services.AddSingleton<TenantPartitionedRateLimiter>();
            },
            configureEndpoints: app =>
            {
                // Mount only the room endpoints to avoid pulling the full presence services
                // (heartbeat recorder, override service, query service, …) into the test host.
                RouteGroupBuilder group = app.MapGranitGroup("presence")
                    .WithTags("Presence - Rooms")
                    .RequireAuthorization();
                group.MapRoomEndpoints();
            });

    private static HttpClient AuthedClient(GranitEndpointTestHost host, Guid userId)
    {
        HttpClient client = host.Application.GetTestClient();
        client.DefaultRequestHeaders.Add(UserIdHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task Heartbeat_returns_room_snapshot_on_success()
    {
        var userId = Guid.NewGuid();
        IResourcePresenceTracker tracker = Substitute.For<IResourcePresenceTracker>();
        tracker
            .JoinAsync(Arg.Any<ResourceRef>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ResourceRoom(SampleResource, [new ResourcePresenceEntry(userId, DateTimeOffset.UtcNow, "{}")]));

        await using GranitEndpointTestHost host = await StartAsync(tracker);
        using HttpClient client = AuthedClient(host, userId);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}/heartbeat",
            new HeartbeatRoomRequest("{}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ResourceRoomResponse? body = await response.Content
            .ReadFromJsonAsync<ResourceRoomResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body!.Kind.ShouldBe(SampleResource.Kind);
        body.Id.ShouldBe(SampleResource.Id);
        body.Participants.Count.ShouldBe(1);
        body.Participants[0].UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task Heartbeat_returns_401_when_caller_has_no_user_claim()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        // No header → anonymous (handler returns NoResult).
        HttpClient client = host.Application.GetTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}/heartbeat",
            new HeartbeatRoomRequest(null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Heartbeat_returns_400_for_invalid_kind()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/presence/rooms/Document/abc/heartbeat", // uppercase kind
            new HeartbeatRoomRequest(null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Heartbeat_returns_422_when_metadata_exceeds_512_bytes()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}/heartbeat",
            new HeartbeatRoomRequest(new string('x', 513)),
            TestContext.Current.CancellationToken);

        // FluentValidation auto-filter surfaces failures as 422 Unprocessable Entity.
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Get_returns_room_snapshot()
    {
        var alice = Guid.NewGuid();
        IResourcePresenceTracker tracker = Substitute.For<IResourcePresenceTracker>();
        tracker
            .GetAsync(Arg.Any<ResourceRef>(), Arg.Any<CancellationToken>())
            .Returns(new ResourceRoom(SampleResource, [new ResourcePresenceEntry(alice, DateTimeOffset.UtcNow, null)]));

        await using GranitEndpointTestHost host = await StartAsync(tracker);
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.GetAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ResourceRoomResponse? body = await response.Content
            .ReadFromJsonAsync<ResourceRoomResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body!.Participants.Count.ShouldBe(1);
        body.Participants[0].UserId.ShouldBe(alice);
    }

    [Fact]
    public async Task Get_returns_404_when_visibility_policy_denies_read()
    {
        var alice = Guid.NewGuid();
        IResourcePresenceTracker tracker = Substitute.For<IResourcePresenceTracker>();
        tracker
            .GetAsync(Arg.Any<ResourceRef>(), Arg.Any<CancellationToken>())
            .Returns(new ResourceRoom(SampleResource, [new ResourcePresenceEntry(alice, DateTimeOffset.UtcNow, null)]));

        DenyAllResourcePresenceVisibilityPolicy denyPolicy = new();

        await using GranitEndpointTestHost host = await StartAsync(tracker, denyPolicy);
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.GetAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_filters_participants_through_visibility_policy()
    {
        var visible = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        IResourcePresenceTracker tracker = Substitute.For<IResourcePresenceTracker>();
        tracker
            .GetAsync(Arg.Any<ResourceRef>(), Arg.Any<CancellationToken>())
            .Returns(new ResourceRoom(SampleResource,
            [
                new ResourcePresenceEntry(visible, DateTimeOffset.UtcNow, null),
                new ResourcePresenceEntry(hidden, DateTimeOffset.UtcNow, null),
            ]));

        WhitelistResourcePresenceVisibilityPolicy whitelist = new(new HashSet<Guid> { visible });

        await using GranitEndpointTestHost host = await StartAsync(tracker, whitelist);
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.GetAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ResourceRoomResponse? body = await response.Content
            .ReadFromJsonAsync<ResourceRoomResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body!.Participants.Select(p => p.UserId).ShouldBe([visible]);
    }

    [Fact]
    public async Task Delete_returns_204_when_user_leaves()
    {
        IResourcePresenceTracker tracker = Substitute.For<IResourcePresenceTracker>();

        await using GranitEndpointTestHost host = await StartAsync(tracker);
        using HttpClient client = AuthedClient(host, Guid.NewGuid());

        HttpResponseMessage response = await client.DeleteAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await tracker.Received(1).LeaveAsync(
            Arg.Is<ResourceRef>(r => r.Kind == SampleResource.Kind && r.Id == SampleResource.Id),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_returns_401_when_unauthenticated()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.Application.GetTestClient();

        HttpResponseMessage response = await client.DeleteAsync(
            $"/presence/rooms/{SampleResource.Kind}/{SampleResource.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ──── Test-only auth handler ────

    private sealed class GuidSubAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out Microsoft.Extensions.Primitives.StringValues raw)
                || string.IsNullOrWhiteSpace(raw))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims = [new Claim("sub", raw.ToString())];
            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    // ──── Visibility policy stubs ────

    private sealed class AllowAllResourcePresenceVisibilityPolicyForTests : IResourcePresenceVisibilityPolicy
    {
        public Task<bool> CanReadRoomAsync(Guid callerUserId, ResourceRef resource, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlySet<Guid>> FilterVisibleParticipantsAsync(
            Guid callerUserId,
            ResourceRef resource,
            IReadOnlyCollection<Guid> participantUserIds,
            CancellationToken cancellationToken)
        {
            IReadOnlySet<Guid> set = new HashSet<Guid>(participantUserIds);
            return Task.FromResult(set);
        }
    }

    private sealed class DenyAllResourcePresenceVisibilityPolicy : IResourcePresenceVisibilityPolicy
    {
        public Task<bool> CanReadRoomAsync(Guid callerUserId, ResourceRef resource, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> FilterVisibleParticipantsAsync(
            Guid callerUserId,
            ResourceRef resource,
            IReadOnlyCollection<Guid> participantUserIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }

    private sealed class WhitelistResourcePresenceVisibilityPolicy(IReadOnlySet<Guid> allowed) : IResourcePresenceVisibilityPolicy
    {
        public Task<bool> CanReadRoomAsync(Guid callerUserId, ResourceRef resource, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<IReadOnlySet<Guid>> FilterVisibleParticipantsAsync(
            Guid callerUserId,
            ResourceRef resource,
            IReadOnlyCollection<Guid> participantUserIds,
            CancellationToken cancellationToken)
        {
            HashSet<Guid> result = [];
            foreach (Guid id in participantUserIds)
            {
                if (allowed.Contains(id))
                {
                    result.Add(id);
                }
            }
            return Task.FromResult<IReadOnlySet<Guid>>(result);
        }
    }
}
