// =============================================================================
// Tests - GranitWolverineSqlServerModule
// =============================================================================
// Verifies module inheritance, DependsOn declarations, and that
// AddGranitWolverineWithSqlServer() registers Wolverine services without throwing.
// =============================================================================

using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Wolverine.Extensions;
using Granit.Wolverine.SqlServer.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.SqlServer.Tests;

public sealed class GranitWolverineSqlServerModuleTests
{
    [Fact]
    public void GranitWolverineSqlServerModule_IsGranitModule() =>
        typeof(GranitWolverineSqlServerModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitWolverineSqlServerModule_IsSealed() =>
        typeof(GranitWolverineSqlServerModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void GranitWolverineSqlServerModule_DependsOn_WolverineModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverineSqlServerModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitWolverineModule)));
    }

    [Fact]
    public void GranitWolverineSqlServerModule_DependsOn_PersistenceModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverineSqlServerModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitPersistenceEntityFrameworkCoreModule)));
    }

    [Fact]
    public void AddGranitWolverineWithSqlServer_RegistersWolverineServices()
    {
        // Provide a minimal in-memory config so PersistMessagesWithSqlServer
        // receives a non-empty connection string (validation fires at app build).
        HostApplicationBuilderSettings settings = new()
        {
            Configuration = new ConfigurationManager(),
        };
        settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wolverine:SqlServer:TransportConnectionString"] =
                "Server=localhost;Database=test;User Id=sa;Password=test;TrustServerCertificate=True",
        });
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);
        builder.AddGranitWolverine();

        Action act = () => builder.AddGranitWolverineWithSqlServer();

        Should.NotThrow(act);
    }
}
