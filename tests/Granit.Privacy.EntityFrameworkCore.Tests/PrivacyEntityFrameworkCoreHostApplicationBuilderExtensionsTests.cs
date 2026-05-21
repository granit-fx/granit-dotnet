using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Privacy.EntityFrameworkCore.Extensions;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.EntityFrameworkCore.Tests;

public sealed class PrivacyEntityFrameworkCoreHostApplicationBuilderExtensionsTests
{
    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton(Substitute.For<ICurrentTenant>());
        return builder;
    }

    [Fact]
    public void AddGranitPrivacyEntityFrameworkCore_RegistersRequiredServices()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitPrivacyEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = builder.Services.BuildServiceProvider();

        provider.GetService<IDbContextFactory<PrivacyDbContext>>().ShouldNotBeNull();

        builder.Services.ShouldContain(d => d.ServiceType == typeof(EfLegalDocumentStore));
    }

    [Fact]
    public void AddGranitPrivacyEntityFrameworkCore_NullBuilder_ThrowsArgumentNullException()
    {
        IHostApplicationBuilder builder = null!;

        Should.Throw<ArgumentNullException>(
            () => builder.AddGranitPrivacyEntityFrameworkCore(_ => { }));
    }

    [Fact]
    public void AddGranitPrivacyEntityFrameworkCore_NullConfigureShared_ThrowsArgumentNullException()
    {
        HostApplicationBuilder builder = CreateBuilder();

        Should.Throw<ArgumentNullException>(
            () => builder.AddGranitPrivacyEntityFrameworkCore(configureShared: null!));
    }

    /// <summary>
    /// Regression: under SchemaPerTenant the registration must wire the schema-keyed factory
    /// so <c>TenantSchemaConnectionInterceptor</c> is active and queries land in the tenant
    /// schema instead of <c>public</c> — same class of bug as the 42P01 fixed for ApiKeys.
    /// </summary>
    [Fact]
    public void AddGranitPrivacyEntityFrameworkCore_SchemaPerTenant_RegistersSchemaKeyedFactory()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitPrivacyEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"),
            configureSchemaPerTenant: options => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = builder.Services.BuildServiceProvider();

        IsolatedDbContextMarker marker = provider
            .GetServices<IsolatedDbContextMarker>()
            .Single(m => m.DbContextType == typeof(PrivacyDbContext));

        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.SchemaPerTenant);
        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.SharedDatabase);

        // Verify the keyed factory descriptor is registered. We don't resolve it because
        // the SchemaPerTenant factory pulls in ITenantSchemaActivator / ITenantSchemaProvider
        // which require GranitPersistenceEntityFrameworkCoreModule to be loaded.
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<PrivacyDbContext>) &&
            d.IsKeyedService &&
            Equals(d.ServiceKey, TenantIsolationStrategy.SchemaPerTenant));
    }

    [Fact]
    public void AddGranitPrivacyEntityFrameworkCore_DatabasePerTenant_RegistersDatabaseKeyedFactory()
    {
        HostApplicationBuilder builder = CreateBuilder();

        builder.AddGranitPrivacyEntityFrameworkCore(
            configureShared: options => options.UseSqlite("DataSource=:memory:"),
            configureDatabasePerTenant: (options, _) => options.UseSqlite("DataSource=:memory:"));

        ServiceProvider provider = builder.Services.BuildServiceProvider();

        IsolatedDbContextMarker marker = provider
            .GetServices<IsolatedDbContextMarker>()
            .Single(m => m.DbContextType == typeof(PrivacyDbContext));

        marker.RegisteredStrategies.ShouldContain(TenantIsolationStrategy.DatabasePerTenant);

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<PrivacyDbContext>) &&
            d.IsKeyedService &&
            Equals(d.ServiceKey, TenantIsolationStrategy.DatabasePerTenant));
    }
}
