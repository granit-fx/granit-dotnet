using Granit.Authorization.EntityFrameworkCore.Extensions;
using Granit.Guids.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Collection fixture that starts a PostgreSQL 17 container and builds a minimal service
/// provider wiring everything the <see cref="GranitRoleOrchestrator"/> needs:
/// ASP.NET Core Identity (with <c>GranitUser</c> / <c>GranitRole</c>), the
/// authorization <see cref="TestHostDbContext"/> backing <c>IRoleMetadataStore</c>, and
/// the orchestrator itself.
/// </summary>
/// <remarks>
/// Uses two separate DbContexts — <see cref="TestIdentityDbContext"/> for Identity rows
/// and <see cref="TestHostDbContext"/> for authorization rows — mirroring the production
/// dual-DbContext shape the compensating-write strategy was designed for. Both point at
/// the same physical database (same connection string) since the orchestrator makes no
/// assumption about database locality.
/// </remarks>
public sealed class RoleOrchestratorTestApplication : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private ServiceProvider? _services;

    public IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Fixture not initialized.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        ServiceCollection services = [];

        services.AddLogging(builder => builder.AddDebug());

        services.AddDbContext<TestIdentityDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));
        services.AddDbContext<TestHostDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));

        // ASP.NET Core Identity — lightweight IdentityCore pipeline is enough for the
        // orchestrator; no cookies / token providers / email services required.
        services.AddIdentityCore<GranitUser>()
            .AddRoles<GranitRole>()
            .AddEntityFrameworkStores<TestIdentityDbContext>();

        // IRoleMetadataStore → EfCoreRoleMetadataStore<TestHostDbContext>
        services.AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>();

        // IGuidGenerator (UuidV7 default) — required by the orchestrator.
        services.AddGranitGuids();

        // The orchestrator itself (internal; visible via InternalsVisibleTo).
        services.AddScoped<IGranitRoleOrchestrator, GranitRoleOrchestrator>();

        _services = services.BuildServiceProvider();

        // Create the schema for both contexts against the same physical database.
        // EnsureCreated on the first context creates its tables; for the second we
        // must call CreateTablesAsync directly — EnsureCreated's HasTables() guard
        // would short-circuit once any EF-managed table exists. This mirrors the
        // standard "multiple DbContexts, one database" workaround.
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();
        await idCtx.Database.EnsureCreatedAsync();
        await hostCtx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Truncates all Identity and authorization rows — call at the start of each test
    /// to guarantee isolation regardless of order. Uses <c>TRUNCATE … CASCADE</c>
    /// rather than <c>EnsureDeleted</c>/<c>EnsureCreated</c> to keep the container warm.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        TestHostDbContext hostCtx = scope.ServiceProvider.GetRequiredService<TestHostDbContext>();

        await hostCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
        await idCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetRoleClaims\", \"AspNetUserClaims\", \"AspNetUserLogins\", \"AspNetUserTokens\", \"AspNetUsers\", \"AspNetRoles\" RESTART IDENTITY CASCADE;");
    }
}
