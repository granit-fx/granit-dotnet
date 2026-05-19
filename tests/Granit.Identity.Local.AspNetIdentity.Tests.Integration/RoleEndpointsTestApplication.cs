using Granit.Authorization.EntityFrameworkCore.Extensions;
using Granit.Guids.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Collection fixture that boots a real <see cref="WebApplication"/> with the
/// <c>/admin/roles</c> endpoints mapped against a PostgreSQL 17 container.
/// Exposes a mutable <see cref="ICurrentTenant"/> so tests can flip between host
/// admin and tenant admin contexts without rebuilding the server.
/// </summary>
/// <remarks>
/// <para>
/// Authentication is handled by <see cref="TestAuthHandler"/> which unconditionally
/// authenticates every request; authorization is deliberately widened via
/// <see cref="PermissiveAuthorizationPolicyProvider"/> so the permission-grant
/// evaluation pipeline (<c>DynamicPermissionPolicyProvider</c> /
/// <c>IPermissionChecker</c>) doesn't gate these tests. That surface is covered
/// by unit tests around <c>PermissionChecker</c>; here we focus on the visibility
/// matrix, tenant-scope role CRUD, and the system-role protection rules applied
/// inside the endpoint handlers.
/// </para>
/// <para>
/// The fixture wires <see cref="TenantAwareRoleLookupNormalizer"/> as the
/// <see cref="ILookupNormalizer"/> so tenant-scope role names (e.g. "Manager"
/// created under both <c>TenantA</c> and <c>TenantB</c>) don't collide on the
/// ASP.NET Core Identity <c>AspNetRoles.NormalizedName</c> unique index.
/// </para>
/// </remarks>
public sealed class RoleEndpointsTestApplication : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private WebApplication? _app;
    private MutableCurrentTenant? _currentTenant;

    public HttpClient HttpClient =>
        _app?.GetTestClient() ?? throw new InvalidOperationException("Fixture not initialized.");

    public MutableCurrentTenant CurrentTenant =>
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

        builder.Services.AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>();
        builder.Services.AddGranitGuids();
        builder.Services.AddScoped<IGranitRoleOrchestrator, GranitRoleOrchestrator>();

        _currentTenant = new MutableCurrentTenant();
        builder.Services.AddSingleton<ICurrentTenant>(_currentTenant);

        // Tenant-scope roles require the tenant-aware normalizer to avoid colliding on
        // AspNetRoles.NormalizedName across tenants (see ADR-023). Granit wires this in
        // GranitIdentityLocalAspNetIdentityModule; the fixture replicates that here since
        // it boots Identity directly with AddIdentityCore.
        builder.Services.Replace(ServiceDescriptor.Scoped<
            ILookupNormalizer, TenantAwareRoleLookupNormalizer>());

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Replace the default policy provider so dynamic permission policy names
        // (e.g. "IdentityLocal.Roles.Read") resolve to a permissive policy.
        builder.Services.AddAuthorization();
        builder.Services.Replace(ServiceDescriptor.Singleton<
            IAuthorizationPolicyProvider, PermissiveAuthorizationPolicyProvider>());

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
    /// Truncates all Identity and authorization rows so each test starts from an
    /// empty schema. The container itself stays warm across tests.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        CurrentTenant.Change(id: null);

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        await hostCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
        await idCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetRoleClaims\", \"AspNetUserClaims\", \"AspNetUserLogins\", \"AspNetUserTokens\", \"AspNetUsers\", \"AspNetRoles\" RESTART IDENTITY CASCADE;");
    }
}
