// =============================================================================
// Tests - AddGranitWolverineWithSqlServer (additional coverage)
// =============================================================================
// Covers configure callback, options validator DI registration, and return
// value for both single-tenant and per-tenant extension methods.
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Wolverine.Extensions;
using Granit.Wolverine.SqlServer.Extensions;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class AddGranitWolverineWithSqlServerTests
{
    private const string ValidTransportConnStr =
        "Server=localhost;Database=wolverine_transport;User Id=sa;Password=test;TrustServerCertificate=True";

    private static HostApplicationBuilder CreateBuilder(
        Dictionary<string, string?>? config = null,
        bool withWolverine = true)
    {
        HostApplicationBuilderSettings settings = new()
        {
            Configuration = new ConfigurationManager(),
        };
        settings.Configuration.AddInMemoryCollection(config ?? new Dictionary<string, string?>
        {
            [$"{WolverineSqlServerOptions.SectionName}:TransportConnectionString"] =
                ValidTransportConnStr,
        });
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);

        if (withWolverine)
        {
            builder.AddGranitWolverine();
        }

        return builder;
    }

    // -----------------------------------------------------------------------
    // Configure callback — invoked synchronously during registration
    // (applied directly on the WolverineOptions instance, not as a deferred
    // extension, to avoid Wolverine 3.0 read-only service collection restriction).
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_WithConfigureCallback_InvokesCallbackSynchronously()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithSqlServer(_ => callbackInvoked = true);

        callbackInvoked.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Options validator registration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_RegistersOptionsValidator()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithSqlServer();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WolverineSqlServerOptions>));
    }

    // -----------------------------------------------------------------------
    // Connection string name — resolved from ConnectionStrings section (Aspire)
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_WithConnectionStringName_DoesNotThrow()
    {
        HostApplicationBuilder builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{WolverineSqlServerOptions.SectionName}:TransportConnectionStringName"] = "catalog-db",
            ["ConnectionStrings:catalog-db"] = ValidTransportConnStr,
        });

        Action act = () => builder.AddGranitWolverineWithSqlServer();

        Should.NotThrow(act);
    }

    // -----------------------------------------------------------------------
    // Missing connection string name — throws
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_WithMissingConnectionStringName_ThrowsInvalidOperationException()
    {
        // Throws before WolverineOptionsHolder lookup, so AddGranitWolverine() is not needed.
        HostApplicationBuilder builder = CreateBuilder(
            config: new Dictionary<string, string?>
            {
                [$"{WolverineSqlServerOptions.SectionName}:TransportConnectionStringName"] = "non-existent-db",
            },
            withWolverine: false);

        Action act = () => builder.AddGranitWolverineWithSqlServer();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("non-existent-db");
    }

    // -----------------------------------------------------------------------
    // Migrate pipeline — no auto-DDL at boot, migrator registered instead
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_RegistersWolverineStoreMigrator()
    {
        // Table creation must go through --migrate mode (IExternalStoreMigrator),
        // never through startup auto-provisioning: concurrent DDL between replicas
        // during a rolling update, and the runtime DB user must not need DDL grants.
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithSqlServer();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IExternalStoreMigrator));
    }

    // -----------------------------------------------------------------------
    // Return value
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_ReturnsSameBuilder()
    {
        HostApplicationBuilder builder = CreateBuilder();

        IHostApplicationBuilder result = builder.AddGranitWolverineWithSqlServer();

        result.ShouldBeSameAs(builder);
    }

    // -----------------------------------------------------------------------
    // PerTenant — configure callback (invoked synchronously)
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_WithConfigureCallback_InvokesCallbackSynchronously()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>(_ => callbackInvoked = true);

        callbackInvoked.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // PerTenant — options validator registration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_RegistersOptionsValidator()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WolverineSqlServerOptions>));
    }

    // -----------------------------------------------------------------------
    // PerTenant — return value
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_ReturnsSameBuilder()
    {
        HostApplicationBuilder builder = CreateBuilder();

        IHostApplicationBuilder result = builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>();

        result.ShouldBeSameAs(builder);
    }
}
