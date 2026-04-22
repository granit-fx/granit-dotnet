using Granit.Authorization.EntityFrameworkCore.Extensions;
using Granit.Guids.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Collection fixture that wires the <see cref="GranitRoleOrchestrator"/> in its
/// <b>atomic shared-connection transaction</b> mode — both
/// <see cref="IIdentityDbContextAccessor"/> and <see cref="IAuthorizationHostDbContextAccessor"/>
/// are registered, and the host DbContext is provided via
/// <see cref="IDbContextFactory{TContext}"/> (required by the accessor).
/// </summary>
/// <remarks>
/// Complements <see cref="RoleOrchestratorTestApplication"/> which exercises the
/// compensating-write fallback (no accessors registered).
/// </remarks>
public sealed class RoleOrchestratorAtomicTestApplication : IAsyncLifetime
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

        // Identity DbContext registered as scoped (required by RoleManager's RoleStore)
        // AND as a factory so the shared-connection accessor can ... actually the identity
        // accessor exposes the scoped instance, so AddDbContext is enough here.
        services.AddDbContext<TestIdentityDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));

        // Host DbContext registered via factory — the atomic path's IAuthorizationHostDbContextAccessor
        // requires IDbContextFactory<THost> to create a fresh context for SetDbConnection.
        services.AddDbContextFactory<TestHostDbContext>(opts =>
            opts.UseNpgsql(_postgres.ConnectionString));

        services.AddIdentityCore<GranitUser>()
            .AddRoles<GranitRole>()
            .AddEntityFrameworkStores<TestIdentityDbContext>();

        services.AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>();
        services.AddGranitGuids();
        services.AddScoped<IGranitRoleOrchestrator, GranitRoleOrchestrator>();

        // Wire the two accessors so the atomic path activates.
        services.AddScoped<IIdentityDbContextAccessor, TestIdentityDbContextAccessor>();
        // IAuthorizationHostDbContextAccessor<TestHostDbContext> is already registered
        // by AddGranitAuthorizationEntityFrameworkCore<TestHostDbContext>() above.

        _services = services.BuildServiceProvider();

        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        IDbContextFactory<TestHostDbContext> hostFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<TestHostDbContext>>();
        await using TestHostDbContext hostCtx = await hostFactory.CreateDbContextAsync();
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

    public async Task ResetDatabaseAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        TestIdentityDbContext idCtx = scope.ServiceProvider.GetRequiredService<TestIdentityDbContext>();
        IDbContextFactory<TestHostDbContext> hostFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<TestHostDbContext>>();
        await using TestHostDbContext hostCtx = await hostFactory.CreateDbContextAsync();

        await hostCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE authorization_role_metadata, authorization_permission_grants RESTART IDENTITY CASCADE;");
        await idCtx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"AspNetUserRoles\", \"AspNetRoleClaims\", \"AspNetUserClaims\", \"AspNetUserLogins\", \"AspNetUserTokens\", \"AspNetUsers\", \"AspNetRoles\" RESTART IDENTITY CASCADE;");
    }

    // Minimal accessor impl that targets the test Identity DbContext.
    private sealed class TestIdentityDbContextAccessor(TestIdentityDbContext context) : IIdentityDbContextAccessor
    {
        public DbContext DbContext => context;
    }
}
