// =============================================================================
// Tests - AddGranitWolverineWithPostgresql (additional coverage)
// =============================================================================
// Covers configure callback, options validator DI registration,
// connection string name resolution, and missing connection string scenarios.
// =============================================================================

using Granit.Wolverine.Extensions;
using Granit.Wolverine.Postgresql.Extensions;
using Granit.Wolverine.Postgresql.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests;

public sealed class AddGranitWolverineWithPostgresqlTests
{
    private const string ValidTransportConnStr =
        "Host=localhost;Database=wolverine_transport;Username=test;Password=test";

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
            [$"{WolverinePostgresqlOptions.SectionName}:TransportConnectionString"] =
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
    public void AddGranitWolverineWithPostgresql_WithConfigureCallback_InvokesCallbackSynchronously()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithPostgresql(_ => callbackInvoked = true);

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
        // Throws before WolverineOptionsHolder lookup, so AddGranitWolverine() is not needed.
        HostApplicationBuilder builder = CreateBuilder(
            config: new Dictionary<string, string?>
            {
                [$"{WolverinePostgresqlOptions.SectionName}:TransportConnectionStringName"] = "non-existent-db",
            },
            withWolverine: false);

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
    // PerTenant — configure callback (invoked synchronously)
    // -----------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverineWithPostgresqlPerTenant_WithConfigureCallback_InvokesCallbackSynchronously()
    {
        HostApplicationBuilder builder = CreateBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverineWithPostgresqlPerTenant<StubTenantDbContext>(_ => callbackInvoked = true);

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
