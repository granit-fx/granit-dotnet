// =============================================================================
// Tests - AddGranitWolverineWithSqlServer (additional coverage)
// =============================================================================
// Covers configure callback, options validator DI registration, and return
// value for both single-tenant and per-tenant extension methods.
// =============================================================================

using Granit.Wolverine.SqlServer.Extensions;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class AddGranitWolverineWithSqlServerTests
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
        return Host.CreateApplicationBuilder(settings);
    }

    // -----------------------------------------------------------------------
    // Configure callback — ConfigureWolverine defers invocation until
    // WolverineOptions is resolved. We verify the IWolverineExtension
    // registration exists (ConfigureWolverine wraps the callback as a
    // LambdaWolverineExtension).
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServer_WithConfigureCallback_RegistersWolverineExtension()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithSqlServer(opts => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWolverineExtension));
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
    // PerTenant — configure callback (deferred by ConfigureWolverine)
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithSqlServerPerTenant_WithConfigureCallback_RegistersWolverineExtension()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithSqlServerPerTenant<StubTenantDbContext>(opts => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWolverineExtension));
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
