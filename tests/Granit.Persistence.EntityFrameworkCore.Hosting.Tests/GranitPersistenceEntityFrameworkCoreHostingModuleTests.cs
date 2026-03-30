using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public sealed class GranitPersistenceEntityFrameworkCoreHostingModuleTests
{
    [Fact]
    public void DependsOn_DeclaresCorrectDependencies()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceEntityFrameworkCoreHostingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
        dependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule));
    }

    [Fact]
    public void ConfigureServices_DoesNotThrow()
    {
        GranitPersistenceEntityFrameworkCoreHostingModule module = new();
        Microsoft.Extensions.Hosting.HostApplicationBuilder builder =
            Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        Should.NotThrow(() => module.ConfigureServices(context));
    }
}
