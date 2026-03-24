using Granit.Modularity;
using Granit.Persistence.Migrations;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Hosting.Tests;

public sealed class GranitPersistenceHostingModuleTests
{
    [Fact]
    public void DependsOn_DeclaresCorrectDependencies()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceHostingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceModule));
        dependedTypes.ShouldContain(typeof(GranitPersistenceMigrationsModule));
    }

    [Fact]
    public void ConfigureServices_DoesNotThrow()
    {
        GranitPersistenceHostingModule module = new();
        Microsoft.Extensions.Hosting.HostApplicationBuilder builder =
            Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        Should.NotThrow(() => module.ConfigureServices(context));
    }
}
