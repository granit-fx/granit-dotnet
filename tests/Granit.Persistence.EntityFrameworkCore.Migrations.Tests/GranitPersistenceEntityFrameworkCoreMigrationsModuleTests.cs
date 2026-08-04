using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
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

    // -------------------------------------------------------------------------
    // NullMigrationLock warning — multi-replica safety signal
    // -------------------------------------------------------------------------

    private static async Task<FakeLogger<GranitPersistenceEntityFrameworkCoreMigrationsModule>> InitializeModuleAsync(
        string environmentName,
        bool useNullLock)
    {
        ServiceCollection services = new();

        if (useNullLock)
        {
            services.AddSingleton<IGranitMigrationLock, NullMigrationLock>();
        }
        else
        {
            services.AddSingleton(Substitute.For<IGranitMigrationLock>());
        }

        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        services.AddSingleton(environment);

        FakeLogger<GranitPersistenceEntityFrameworkCoreMigrationsModule> logger = new();
        services.AddSingleton<ILogger<GranitPersistenceEntityFrameworkCoreMigrationsModule>>(logger);

        await using ServiceProvider provider = services.BuildServiceProvider();
        await new GranitPersistenceEntityFrameworkCoreMigrationsModule()
            .OnApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        return logger;
    }

    [Fact]
    public async Task OnApplicationInitialization_NullLockOutsideDevelopment_LogsWarning()
    {
        FakeLogger<GranitPersistenceEntityFrameworkCoreMigrationsModule> logger =
            await InitializeModuleAsync(Environments.Production, useNullLock: true);

        logger.Collector.GetSnapshot().ShouldContain(
            r => r.Level == LogLevel.Warning && r.Message.Contains("NullMigrationLock"));
    }

    [Fact]
    public async Task OnApplicationInitialization_NullLockInDevelopment_DoesNotWarn()
    {
        FakeLogger<GranitPersistenceEntityFrameworkCoreMigrationsModule> logger =
            await InitializeModuleAsync(Environments.Development, useNullLock: true);

        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task OnApplicationInitialization_RealLockOutsideDevelopment_DoesNotWarn()
    {
        FakeLogger<GranitPersistenceEntityFrameworkCoreMigrationsModule> logger =
            await InitializeModuleAsync(Environments.Production, useNullLock: false);

        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }
}
