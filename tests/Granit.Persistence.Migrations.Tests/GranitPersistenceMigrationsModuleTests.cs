using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Migrations.Tests;

public sealed class GranitPersistenceMigrationsModuleTests
{
    [Fact]
    public void DependsOn_DeclaresCorrectDependencies()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitPersistenceMigrationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()];

        attributes.Length.ShouldBe(1);
        Type[] dependedTypes = attributes[0].DependedTypes;
        dependedTypes.ShouldContain(typeof(GranitPersistenceModule));
        dependedTypes.Length.ShouldBe(1);
    }

    [Fact]
    public void ConfigureServices_RegistersMigrationCycleRegistry()
    {
        GranitPersistenceMigrationsModule module = new();
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
        GranitPersistenceMigrationsModule module = new();
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
        GranitPersistenceMigrationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantEnumerator));
    }

    [Fact]
    public void ConfigureServices_RegistersMigrationBatchDispatcher()
    {
        GranitPersistenceMigrationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IMigrationBatchDispatcher));
    }
}
