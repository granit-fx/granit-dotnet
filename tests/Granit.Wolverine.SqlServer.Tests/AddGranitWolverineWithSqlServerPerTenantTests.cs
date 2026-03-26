// =============================================================================
// Tests - AddGranitWolverineWithSqlServerPerTenant<TContext>
// =============================================================================
// Verifies that the extension method registers the per-tenant factory and DbContext
// as Scoped, and that the extension method itself does not throw during registration.
// DI registrations are inspected directly on IServiceCollection — the host is not
// built to avoid requiring a live SQL Server Outbox connection.
// =============================================================================

using Granit.Wolverine.Extensions;
using Granit.Wolverine.SqlServer.Extensions;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

// Minimal DbContext stub used across Wolverine.SqlServer test files.
internal sealed class StubTenantDbContext(DbContextOptions<StubTenantDbContext> options)
    : DbContext(options);

public sealed class AddGranitWolverineWithSqlServerPerTenantTests
{
    private const string ValidTransportConnStr =
        "Server=localhost;Database=wolverine_transport;User Id=sa;Password=test;TrustServerCertificate=True";

    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilderSettings settings = new()
        {
            Configuration = new ConfigurationManager(),
        };
        settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{WolverineSqlServerOptions.SectionName}:TransportConnectionString"] =
                ValidTransportConnStr,
        });
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);
        builder.AddGranitWolverine();
        return builder;
    }

    // -----------------------------------------------------------------------
    // Registration — no throw
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_DoesNotThrow()
    {
        HostApplicationBuilder builder = CreateBuilder();

        Action act = () => builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        Should.NotThrow(act);
    }

    // -----------------------------------------------------------------------
    // Registration — IDbContextFactory<TContext>
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_RegistersDbContextFactory_AsScoped()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            s => s.ServiceType == typeof(IDbContextFactory<StubTenantDbContext>));

        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_RegistersDbContextFactory_WithImplementation()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            s => s.ServiceType == typeof(IDbContextFactory<StubTenantDbContext>));

        // The concrete factory type is internal to Granit.Persistence; we verify
        // a named implementation type is registered (not a factory lambda).
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldNotBeNull();
        descriptor.ImplementationType!.Name.ShouldContain("TenantPerDatabase");
    }

    // -----------------------------------------------------------------------
    // Registration — TContext
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_RegistersDbContext_AsScoped()
    {
        HostApplicationBuilder builder = CreateBuilder();
        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            s => s.ServiceType == typeof(StubTenantDbContext));

        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    // -----------------------------------------------------------------------
    // Registration — TryAdd preserves an existing factory registration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_PreservesExistingFactory_WhenAlreadyRegistered()
    {
        HostApplicationBuilder builder = CreateBuilder();

        // Pre-register a custom factory (e.g., from integration test setup) via lambda.
        builder.Services.AddScoped<IDbContextFactory<StubTenantDbContext>>(
            static _ => null!);

        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        // TryAdd must not have replaced the existing lambda registration.
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            s => s.ServiceType == typeof(IDbContextFactory<StubTenantDbContext>));

        // Lambda registration has no ImplementationType (it's null).
        descriptor!.ImplementationType.ShouldBeNull();
    }
}
