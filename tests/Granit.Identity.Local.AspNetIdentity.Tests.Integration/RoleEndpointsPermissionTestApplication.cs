using System.Diagnostics.Metrics;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.Extensions;
using Granit.Authorization.Extensions;
using Granit.Guids.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Collection fixture for permission-grant enforcement tests on <c>/admin/roles</c>.
/// Unlike <see cref="RoleEndpointsTestApplication"/> — which short-circuits the
/// authorization pipeline via <see cref="PermissiveAuthorizationPolicyProvider"/> to
/// focus on the visibility matrix — this fixture wires the **real** Granit.Authorization
/// stack (<c>DynamicPermissionPolicyProvider</c> + <c>PermissionAuthorizationHandler</c>
/// + <c>PermissionChecker</c>) so tests can exercise the user/role/client grant
/// resolution path end-to-end against real PostgreSQL.
/// </summary>
public sealed class RoleEndpointsPermissionTestApplication : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private WebApplication? _app;
    private MutableCurrentTenant? _currentTenant;

    internal const string TestUserId = "user-test-42";

    public MutableCurrentTenant CurrentTenant =>
        _currentTenant ?? throw new InvalidOperationException("Fixture not initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddDbContext<TestIdentityDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));
        builder.Services.AddDbContext<TestHostDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));

        builder.Services.AddIdentityCore<GranitUser>()
            .AddRoles<GranitRole>()
            .AddEntityFrameworkStores<TestIdentityDbContext>();

        builder.Services.Replace(ServiceDescriptor.Scoped<
            ILookupNormalizer, TenantAwareRoleLookupNormalizer>());

        // Full Granit.Authorization wiring — policy provider + permission checker +
        // grant providers (user / role / client) + EF-backed stores. AddAuthorization
        // itself must be called (registers the middleware's plumbing); AddGranitAuthorization
        // only adds the policy provider + handlers.
        builder.Services.AddAuthorization();
        builder.Services.AddGranitAuthorization();
        builder.Services.AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>();
        builder.Services.AddSingleton<IPermissionDefinitionProvider, TestPermissionDefinitionProvider>();

        builder.Services.AddGranitGuids();
        builder.Services.AddScoped<IGranitRoleOrchestrator, GranitRoleOrchestrator>();

        _currentTenant = new MutableCurrentTenant();
        builder.Services.AddSingleton<ICurrentTenant>(_currentTenant);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        // FusionCache backs the PermissionChecker. In-memory default with a short
        // duration to avoid cross-test bleed; ResetAsync also purges it explicitly.
        builder.Services.AddFusionCache();

        builder.Services.TryAddSingleton<IMeterFactory>(_ => new NoopMeterFactory());

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SubjectTestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Do NOT replace IAuthorizationPolicyProvider — we want the real
        // DynamicPermissionPolicyProvider registered by AddGranitAuthorization().

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGranitRoles();

        await using (AsyncServiceScope scope = _app.Services.CreateAsyncScope())
        {
            TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
            TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();
            await idCtx.Database.EnsureCreatedAsync();
            await hostCtx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Builds an <see cref="HttpClient"/> that authenticates every request as
    /// <paramref name="userId"/> via the <see cref="SubjectTestAuthHandler"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = TestUserId)
    {
        HttpClient client = _app?.GetTestClient()
            ?? throw new InvalidOperationException("Fixture not initialized.");
        client.DefaultRequestHeaders.Add(SubjectTestAuthHandler.SubjectHeader, userId);
        return client;
    }

    /// <summary>Builds an <see cref="HttpClient"/> with no auth header (anonymous).</summary>
    public HttpClient CreateAnonymousClient() =>
        _app?.GetTestClient() ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <summary>Seeds a user-scope permission grant row directly via the host DbContext.</summary>
    public async Task GrantToUserAsync(
        string userId, string permissionName, Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _app!.Services.CreateAsyncScope();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        var grant = PermissionGrant.Create(
            id: Guid.NewGuid(),
            permissionName: permissionName,
            providerName: PermissionGrantProviderNames.User,
            providerKey: userId,
            tenantId: tenantId);

        hostCtx.PermissionGrants.Add(grant);
        await hostCtx.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Seeds a <see cref="RoleMetadata"/> row directly (no Identity counterpart).</summary>
    public async Task<RoleMetadata> SeedRoleMetadataAsync(
        string name, MultiTenancySide side, Guid? tenantId, bool isSystem = false,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _app!.Services.CreateAsyncScope();
        IRoleMetadataStore store = scope.ServiceProvider.GetRequiredService<IRoleMetadataStore>();

        var metadata = RoleMetadata.Create(
            id: Guid.NewGuid(),
            name: name,
            multiTenancySide: side,
            tenantId: tenantId,
            clientId: null,
            description: null,
            isSystem: isSystem);

        await store.AddAsync(metadata, cancellationToken);
        return metadata;
    }

    /// <summary>
    /// Truncates all grants / metadata / identity rows AND flushes the permission
    /// checker cache so each test starts from a clean slate.
    /// </summary>
    public async Task ResetAsync()
    {
        CurrentTenant.Change(id: null);

        await using AsyncServiceScope scope = _app!.Services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        await hostCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
        await idCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetRoleClaims\", \"AspNetUserClaims\", \"AspNetUserLogins\", \"AspNetUserTokens\", \"AspNetUsers\", \"AspNetRoles\" RESTART IDENTITY CASCADE;");

        IFusionCache cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();
        await cache.ClearAsync();
    }

    /// <summary>Minimal no-op meter factory — only required because PermissionChecker resolves AuthorizationMetrics.</summary>
    private sealed class NoopMeterFactory : IMeterFactory
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
