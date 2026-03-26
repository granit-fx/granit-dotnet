// =============================================================================
// Tests - GranitWolverinePostgresqlModule
// =============================================================================
// Verifies module inheritance, DependsOn declarations, and that
// AddGranitWolverineWithPostgresql() registers Wolverine services without throwing.
// =============================================================================

using Granit.Modularity;
using Granit.Persistence;
using Granit.Wolverine.Extensions;
using Granit.Wolverine.Postgresql.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Postgresql.Tests;

public sealed class GranitWolverinePostgresqlModuleTests
{
    [Fact]
    public void GranitWolverinePostgresqlModule_IsGranitModule() =>
        typeof(GranitWolverinePostgresqlModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitWolverinePostgresqlModule_IsSealed() =>
        typeof(GranitWolverinePostgresqlModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void GranitWolverinePostgresqlModule_DependsOn_WolverineModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverinePostgresqlModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitWolverineModule)));
    }

    [Fact]
    public void GranitWolverinePostgresqlModule_DependsOn_PersistenceModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverinePostgresqlModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitPersistenceModule)));
    }

    [Fact]
    public void AddGranitWolverineWithPostgresql_RegistersWolverineServices()
    {
        // Provide a minimal in-memory config so PersistMessagesWithPostgresql
        // receives a non-empty connection string (validation fires at app build).
        HostApplicationBuilderSettings settings = new()
        {
            Configuration = new ConfigurationManager(),
        };
        settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WolverinePostgresql:TransportConnectionString"] =
                "Host=localhost;Database=test;Username=test;Password=test",
        });
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(settings);
        builder.AddGranitWolverine();

        Action act = () => builder.AddGranitWolverineWithPostgresql();

        Should.NotThrow(act);
    }
}
