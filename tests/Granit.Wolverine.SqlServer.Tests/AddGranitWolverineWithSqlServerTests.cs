// =============================================================================
// Tests - AddGranitWolverineWithSqlServer (additional coverage)
// =============================================================================
// Covers configure callback, options validator DI registration, and return
// value for both single-tenant and per-tenant extension methods.
// =============================================================================

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
