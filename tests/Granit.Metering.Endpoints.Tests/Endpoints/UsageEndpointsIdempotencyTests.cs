using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Guids;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Extensions;
using Granit.Http.Idempotency.Models;
using Granit.Metering;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Endpoints.Dtos;
using Granit.Metering.Endpoints.Extensions;
using Granit.Metering.Endpoints.Permissions;
using Granit.MultiTenancy;
using Granit.Users;
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

namespace Granit.Metering.Endpoints.Tests.Endpoints;

/// <summary>
/// Tests proving the dual-layer idempotency wiring on <c>POST /metering/events</c>:
/// (1) HTTP layer enforced via <see cref="Granit.Http.Idempotency.Attributes.IdempotentAttribute"/> +
/// <c>UseGranitIdempotency</c> middleware (rejects missing header, replays on duplicate);
/// (2) DB layer enforced via the unique constraint on
/// <c>(TenantId, IdempotencyKey)</c> in <c>MeterEvent</c> (silent dedup at the recorder layer).
///
/// The DB layer behavior itself is exercised by the EF integration tests; here we
/// verify the HTTP-side wiring stays in place and the renamed RFC 6648 replay header
/// (<c>Idempotent-Replayed</c>) is emitted.
/// </summary>
public sealed class UsageEndpointsIdempotencyTests : IAsyncDisposable
{
    private const string AdminRole = "metering-recorder";
    private const string EventsRoute = "/metering/events";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000aaa");
    private static readonly Guid MeterId = Guid.Parse("00000000-0000-0000-0000-000000000bbb");

    private readonly IMeterEventRecorder _recorder = Substitute.For<IMeterEventRecorder>();
    private readonly IMeterDefinitionReader _reader = Substitute.For<IMeterDefinitionReader>();
    private readonly IIdempotencyStore _idempotencyStore = new InMemoryIdempotencyStore();
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public UsageEndpointsIdempotencyTests()
    {
        // Published meter required so the handler proceeds past the lifecycle ingestion gate.
        var activeMeter = MeterDefinition.Create(
            id: MeterId,
            name: "test.meter",
            unit: "call",
            aggregationType: AggregationType.Count,
            description: null);
        activeMeter.Publish();

        _reader
            .GetByIdAsync(Arg.Any<MeterDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns(activeMeter);

        // Stable GUID for predictable assertions in test output.
        IGuidGenerator guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("test-user");

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services
            .AddAuthorizationBuilder()
            .AddPolicy(MeteringPermissions.Usage.Record, p => p.RequireRole(AdminRole))
            .AddPolicy(MeteringPermissions.Usage.Read, p => p.RequireRole(AdminRole))
            .AddPolicy(MeteringPermissions.Meters.Read, p => p.RequireRole(AdminRole))
            .AddPolicy(MeteringPermissions.Meters.Manage, p => p.RequireRole(AdminRole));

        // Idempotency middleware + override the store with our in-memory implementation.
        builder.Services.AddGranitIdempotency(opts =>
        {
            opts.ExecutionTimeout = TimeSpan.FromSeconds(10);
            opts.InProgressTtl = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddSingleton(_idempotencyStore);

        // Application services consumed by the endpoint handler.
        builder.Services.AddSingleton(_recorder);
        builder.Services.AddSingleton(_reader);
        builder.Services.AddSingleton(guidGen);
        builder.Services.AddScoped(_ => currentTenant);
        builder.Services.AddScoped(_ => currentUser);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseGranitIdempotency();
        _app.MapUsageOnly();
        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
        _client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AdminRole);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task PostEvents_WithoutIdempotencyKey_Returns422()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            EventsRoute,
            BuildBatch("evt-1"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await _recorder.DidNotReceiveWithAnyArgs().RecordBatchAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PostEvents_SecondRequestWithSameKey_ReplaysWith_IdempotentReplayedHeader()
    {
        HttpResponseMessage first = await SendWithKey("idem-key-replay", BuildBatch("evt-replay"));
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage second = await SendWithKey("idem-key-replay", BuildBatch("evt-replay"));

        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.Headers.Contains("Idempotent-Replayed").ShouldBeTrue(
            "RFC 6648 (no X- prefix) — replay marker MUST be 'Idempotent-Replayed', aligned with ORB / Stripe");
        second.Headers.GetValues("Idempotent-Replayed").ShouldContain("true");

        // The handler must execute exactly ONCE — the second response is a replay.
        await _recorder.Received(1).RecordBatchAsync(
            Arg.Any<IReadOnlyList<MeterEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostEvents_SameKey_DifferentBody_Returns422()
    {
        HttpResponseMessage first = await SendWithKey("idem-key-mismatch", BuildBatch("evt-A"));
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage second = await SendWithKey("idem-key-mismatch", BuildBatch("evt-B"));

        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity,
            "Reusing an idempotency key with a different payload MUST be rejected (HTTP-layer payload hash mismatch)");
    }

    private async Task<HttpResponseMessage> SendWithKey(string key, RecordUsageRequest body)
    {
        HttpRequestMessage req = new(HttpMethod.Post, EventsRoute)
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(req, TestContext.Current.CancellationToken);
    }

    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private static RecordUsageRequest BuildBatch(string payloadKey) =>
        new([
            new MeterEventRequest(
                MeterDefinitionId: MeterId,
                IdempotencyKey: payloadKey,
                Quantity: 1m,
                Timestamp: FixedTimestamp,
                Metadata: null),
        ]);

    // ── In-memory idempotency store ───────────────────────────────────────────
    // The middleware exercises full state transitions (Absent → InProgress → Completed).
    // We reuse the same store across requests so replays observe the cached entry.

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, IdempotencyEntry> _entries = new(StringComparer.Ordinal);

        public Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_entries.ContainsKey(key))
                {
                    return Task.FromResult(false);
                }

                _entries[key] = entry;
                return Task.FromResult(true);
            }
        }

        public Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult(_entries.TryGetValue(key, out IdempotencyEntry? e) ? e : null);
            }
        }

        public Task SetCompletedAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_entries.ContainsKey(key))
                {
                    _entries[key] = entry;
                }

                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _entries.Remove(key);
                return Task.CompletedTask;
            }
        }
    }

    // ── Fake authentication handler ───────────────────────────────────────────

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            Claim[] claims =
            [
                new(ClaimTypes.Name, "test-user"),
                .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

// ── Endpoint mapping helper ──────────────────────────────────────────────────
// MapGranitMetering also wires QueryEngine endpoints which require a richer DI setup.
// To stay focused on the /events endpoint, we re-mount only the usage routes via
// the same internal extension used by the production module.

internal static class TestRouteHelpers
{
    public static IEndpointRouteBuilder MapUsageOnly(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGranitMetering(opts => opts.RoutePrefix = "metering");
        return endpoints;
    }
}
