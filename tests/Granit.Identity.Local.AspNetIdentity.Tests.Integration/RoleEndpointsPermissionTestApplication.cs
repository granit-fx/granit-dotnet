using Granit.Authorization.Domain;
using Granit.Authorization.EntityFrameworkCore.Extensions;
using Granit.Authorization.Extensions;
using Granit.Guids.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.Testing.Fakes;
using Granit.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Fixture for the <c>/admin/roles</c> endpoints wired with the REAL
/// <c>AddGranitAuthorization()</c> + full policy provider / permission-grant
/// pipeline (<c>DynamicPermissionPolicyProvider</c> →
/// <c>PermissionAuthorizationHandler</c> → <c>PermissionChecker</c>). Unlike
/// <see cref="RoleEndpointsTestApplication"/> which uses
/// <c>PermissiveAuthorizationPolicyProvider</c>, this fixture exercises the
/// enforcement pipeline end-to-end against real PostgreSQL.
/// </summary>
/// <remarks>
/// Authentication is driven by <see cref="PermissionTestAuthHandler"/> — tests
/// flip the caller identity via the <c>X-Test-User-Id</c> and
/// <c>X-Test-Roles</c> headers. Permission grants are seeded directly on the
/// host DbContext via <see cref="SeedUserGrantAsync"/>.
/// </remarks>
public sealed class RoleEndpointsPermissionTestApplication : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private WebApplication? _app;
    private SharedCurrentTenant? _currentTenant;

    public HttpClient HttpClient =>
        _app?.GetTestClient() ?? throw new InvalidOperationException("Fixture not initialized.");

    public SharedCurrentTenant CurrentTenant =>
        _currentTenant ?? throw new InvalidOperationException("Fixture not initialized.");

    public IServiceProvider Services =>
        _app?.Services ?? throw new InvalidOperationException("Fixture not initialized.");

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

        builder.Services.AddIdentityCore<LocalIdentity>()
            .AddRoles<GranitRole>()
            .AddEntityFrameworkStores<TestIdentityDbContext>();

        // Tenant-scope role names require the tenant-aware normalizer (ADR-023).
        builder.Services.Replace(ServiceDescriptor.Scoped<
            ILookupNormalizer, TenantAwareRoleLookupNormalizer>());

        builder.Services.AddGranitGuids();
        builder.Services.AddScoped<IGranitRoleOrchestrator, GranitRoleOrchestrator>();

        _currentTenant = new SharedCurrentTenant();
        builder.Services.AddSingleton<ICurrentTenant>(_currentTenant);

        // Real permission pipeline — no PermissiveAuthorizationPolicyProvider here.
        builder.Services.AddGranitAuthorization();
        builder.Services.AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>();

        // Register the IPermissionDefinitionProvider that GranitAuthorizationModule would
        // auto-discover in a module-loader host — here we construct the DI container
        // manually so the provider needs an explicit registration. The provider is
        // internal; InternalsVisibleTo from Granit.Identity.Local.Endpoints makes it
        // reachable.
        builder.Services.AddSingleton<Granit.Authorization.IPermissionDefinitionProvider,
            Granit.Identity.Local.Endpoints.Permissions.IdentityLocalPermissionDefinitionProvider>();

        // ICurrentUserService reads the principal populated by PermissionTestAuthHandler.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        // FusionCache dependency of PermissionChecker — plain in-memory instance; no
        // Granit.Caching multi-tenant decorator needed for these tests.
        builder.Services.AddSingleton<IFusionCache>(new FusionCache(new FusionCacheOptions()));

        builder.Services
            .AddAuthentication(PermissionTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, PermissionTestAuthHandler>(
                PermissionTestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

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

    /// <summary>Resets DB state, cache entries, and tenant context so each test starts empty.</summary>
    public async Task ResetAsync()
    {
        CurrentTenant.Change(id: null);

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        await hostCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
        await idCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetRoleClaims\", \"AspNetUserClaims\", \"AspNetUserLogins\", \"AspNetUserTokens\", \"AspNetUsers\", \"AspNetRoles\" RESTART IDENTITY CASCADE;");

        // Drop PermissionChecker's cache so previously-granted entries from the last test
        // don't leak across the boundary (the cache is a singleton on the fixture).
        IFusionCache cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();
        await cache.ClearAsync();
    }

    /// <summary>Seeds a user-scope permission grant for <paramref name="userId"/>.</summary>
    public async Task SeedUserGrantAsync(string userId, string permissionName, Guid? tenantId = null)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        TestHostDbContext ctx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        var grant = PermissionGrant.Create(
            id: Guid.NewGuid(),
            permissionName: permissionName,
            providerName: "U",
            providerKey: userId,
            tenantId: tenantId);

        ctx.PermissionGrants.Add(grant);
        await ctx.SaveChangesAsync();
    }
}
