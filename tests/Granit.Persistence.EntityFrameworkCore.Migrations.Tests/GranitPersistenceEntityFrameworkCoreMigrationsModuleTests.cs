using Granit.Modularity;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class GranitPersistenceEntityFrameworkCoreMigrationsModuleTests
{
    [Fact]
    public void DependsOn_DeclaresCorrectDependencies()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
        dependedTypes.Length.ShouldBe(1);
    }

    [Fact]
    public void ConfigureServices_RegistersMigrationCycleRegistry()
    {
        GranitPersistenceEntityFrameworkCoreMigrationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IMigrationCycleRegistry));
    }

    [Fact]
    public void ConfigureServices_RegistersTenantDbIsolator()
    {
        GranitPersistenceEntityFrameworkCoreMigrationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantDbIsolator));
    }

    [Fact]
    public void ConfigureServices_RegistersTenantEnumerator()
    {
        GranitPersistenceEntityFrameworkCoreMigrationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantEnumerator));
    }

}
