using Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

public sealed class ApiKeysEntityFrameworkCoreServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateBaseServices()
    {
        var services = new ServiceCollection();
        // AddGranitIsolatedDbContext binds TenantIsolationOptions from configuration
        // and the IsolatedDbContextFactory logs through ILogger<>. Tests need both.
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        return services;
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_RegistersRequiredServices()
    {
        ServiceCollection services = CreateBaseServices();

        services.AddGranitApiKeysEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IDbContextFactory<AuthenticationApiKeysDbContext>>().ShouldNotBeNull();
        provider.GetService<IApiKeyStore>().ShouldNotBeNull();
        provider.GetService<IApiKeyAdminStore>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(
            () => services.AddGranitApiKeysEntityFrameworkCore(_ => { }));
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_NullConfigureShared_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(
            () => services.AddGranitApiKeysEntityFrameworkCore(configureShared: null!));
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_DoesNotDuplicateOnSecondCall()
    {
        var services = new ServiceCollection();

        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));
        services.AddGranitApiKeysEntityFrameworkCore(
            options => options.UseSqlite("DataSource=:memory:"));

        // TryAddScoped should prevent duplicate store registrations
        services.Count(s => s.ServiceType == typeof(IApiKeyStore)).ShouldBe(1);
        services.Count(s => s.ServiceType == typeof(IApiKeyAdminStore)).ShouldBe(1);
    }

    /// <summary>
    /// Regression: under SchemaPerTenant the registration must wire the schema-keyed factory
    /// so <c>TenantSchemaConnectionInterceptor</c> is active and queries land in the tenant
    /// schema instead of <c>public</c> (repro: granit-iot-showcase 42P01 on /api/api-keys).
    /// </summary>
    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_SchemaPerTenant_RegistersSchemaKeyedFactory()
    {
        ServiceCollection services = CreateBaseServices();

        services.AddGranitApiKeysEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"),
            configureSchemaPerTenant: options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();

        IsolatedDbContextMarker marker = provider
            .GetServices<IsolatedDbContextMarker>()
            .Single(m => m.DbContextType == typeof(AuthenticationApiKeysDbContext));

        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.SchemaPerTenant);
        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.SharedDatabase);

        // Verify the keyed factory descriptor is registered. We don't resolve it because
        // the SchemaPerTenant factory pulls in ITenantSchemaActivator / ITenantSchemaProvider
        // which require GranitPersistenceEntityFrameworkCoreModule to be loaded — out of scope
        // for a unit test of the extension method.
        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<AuthenticationApiKeysDbContext>) &&
            d.IsKeyedService &&
            Equals(d.ServiceKey, TenantIsolationStrategy.SchemaPerTenant));
    }

    [Fact]
    public void AddGranitApiKeysEntityFrameworkCore_DatabasePerTenant_RegistersDatabaseKeyedFactory()
    {
        ServiceCollection services = CreateBaseServices();

        services.AddGranitApiKeysEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"),
            configureDatabasePerTenant: (options, _) => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = services.BuildServiceProvider();

        IsolatedDbContextMarker marker = provider
            .GetServices<IsolatedDbContextMarker>()
            .Single(m => m.DbContextType == typeof(AuthenticationApiKeysDbContext));

        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.DatabasePerTenant);

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<AuthenticationApiKeysDbContext>) &&
            d.IsKeyedService &&
            Equals(d.ServiceKey, TenantIsolationStrategy.DatabasePerTenant));
    }
}
