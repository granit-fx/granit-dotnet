// =============================================================================
// Tests - AddGranitWolverineWithPostgresql (additional coverage)
// =============================================================================
// Covers configure callback, options validator DI registration,
// connection string name resolution, and missing connection string scenarios.
// =============================================================================

using Granit.Wolverine.Postgresql.Extensions;
using Granit.Wolverine.Postgresql.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests;

public sealed class AddGranitWolverineWithPostgresqlTests
{
    private const string ValidTransportConnStr =
        "Host=localhost;Database=wolverine_transport;Username=test;Password=test";

    private static HostApplicationBuilder CreateBuilder(
        Dictionary<string, string?>? config = null)
    {
        HostApplicationBuilderSettings settings = new()
        {
            Configuration = new ConfigurationManager(),
        };
        settings.Configuration.AddInMemoryCollection(config ?? new Dictionary<string, string?>
        {
            [$"{WolverinePostgresqlOptions.SectionName}:TransportConnectionString"] =
                ValidTransportConnStr,
        });
        return Host.CreateApplicationBuilder(settings);
    }

    // -----------------------------------------------------------------------
    // Configure callback
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresql_WithConfigureCallback_InvokesCallback()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithPostgresql(opts => { callbackInvoked = true; });

        callbackInvoked.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Options validator registration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresql_RegistersOptionsValidator()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithPostgresql();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WolverinePostgresqlOptions>));
    }

    // -----------------------------------------------------------------------
    // Connection string name — resolved from ConnectionStrings section
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresql_WithConnectionStringName_DoesNotThrow()
    {
        HostApplicationBuilder builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{WolverinePostgresqlOptions.SectionName}:TransportConnectionStringName"] = "catalog-db",
            ["ConnectionStrings:catalog-db"] = ValidTransportConnStr,
        });

        Action act = () => builder.AddGranitWolverineWithPostgresql();

        Should.NotThrow(act);
    }

    // -----------------------------------------------------------------------
    // Missing connection string name — throws
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresql_WithMissingConnectionStringName_ThrowsInvalidOperationException()
    {
        HostApplicationBuilder builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"{WolverinePostgresqlOptions.SectionName}:TransportConnectionStringName"] = "non-existent-db",
        });

        Action act = () => builder.AddGranitWolverineWithPostgresql();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("non-existent-db");
    }

    // -----------------------------------------------------------------------
    // Return value
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresql_ReturnsSameBuilder()
    {
        HostApplicationBuilder builder = CreateBuilder();

        IHostApplicationBuilder result = builder.AddGranitWolverineWithPostgresql();

        result.ShouldBeSameAs(builder);
    }

    // -----------------------------------------------------------------------
    // PerTenant — configure callback
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresqlPerTenant_WithConfigureCallback_InvokesCallback()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithPostgresqlPerTenant<StubTenantDbContext>(
            opts => { callbackInvoked = true; });

        callbackInvoked.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // PerTenant — options validator registration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresqlPerTenant_RegistersOptionsValidator()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitWolverineWithPostgresqlPerTenant<StubTenantDbContext>();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WolverinePostgresqlOptions>));
    }
}
