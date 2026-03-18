using Granit.Persistence.Extensions;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

public sealed class PersistenceTenantExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    // -------------------------------------------------------------------------
    // AddTenantPerDatabaseDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddTenantPerDatabaseDbContext_RegistersFactory_Scoped()
    {
        ServiceCollection services = new();

        services.AddTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<TestDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddTenantPerDatabaseDbContext_RegistersContext_Scoped()
    {
        ServiceCollection services = new();

        services.AddTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.ShouldContain(d =>
            d.ServiceType == typeof(TestDbContext) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddTenantPerDatabaseDbContext_TryAdd_DoesNotOverridePreRegistered()
    {
        ServiceCollection services = new();
        services.AddScoped<IDbContextFactory<TestDbContext>>(
            _ => null!); // pre-register
        services.AddTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.Count(d => d.ServiceType == typeof(IDbContextFactory<TestDbContext>))
                .ShouldBe(1, "TryAddScoped must not override pre-registered factory");
    }

    [Fact]
    public void AddTenantPerDatabaseDbContext_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddTenantPerDatabaseDbContext<TestDbContext>(
            (opts, cs) => opts.UseInMemoryDatabase(cs));

        result.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // AddTenantPerSchemaDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddTenantPerSchemaDbContext_RegistersFactory_Scoped()
    {
        ServiceCollection services = new();

        services.AddTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<TestDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddTenantPerSchemaDbContext_RegistersDefaultSchemaProvider()
    {
        ServiceCollection services = new();

        services.AddTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITenantSchemaProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddTenantPerSchemaDbContext_WithSchemaOptions_RegistersOptions()
    {
        ServiceCollection services = new();

        services.AddTenantPerSchemaDbContext<TestDbContext>(
            opts => opts.UseInMemoryDatabase("shared-db"),
            schema => schema.Prefix = "t_");

        services.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddTenantPerSchemaDbContext_DoesNotRegisterDefaultSchemaActivator()
    {
        // No default ITenantSchemaActivator since Granit.Persistence.Postgres was
        // decoupled from the generic persistence package. Callers must invoke
        // AddGranitPostgres() (or register their own implementation) before this call.
        ServiceCollection services = new();

        services.AddTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldNotContain(d => d.ServiceType == typeof(ITenantSchemaActivator));
    }

    [Fact]
    public void AddTenantPerSchemaDbContext_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddTenantPerSchemaDbContext<TestDbContext>(
            opts => opts.UseInMemoryDatabase("shared-db"));

        result.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // AddGranitIsolatedDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitIsolatedDbContext_RegistersIsolationOptions()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<TenantIsolationOptions>));
    }

    [Fact]
    public void AddGranitIsolatedDbContext_RegistersFacadeFactory_Scoped()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<TestDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIsolatedDbContext_RegistersContext_Scoped()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(TestDbContext) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIsolatedDbContext_RegistersDefaultStrategyProvider()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITenantIsolationStrategyProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitIsolatedDbContext_WithDatabasePerTenant_RegistersKeyedFactory()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"),
            configureDatabasePerTenant: (opts, cs) => opts.UseInMemoryDatabase(cs));

        // Should have multiple keyed registrations
        services.Count(d => d.ServiceType == typeof(IDbContextFactory<TestDbContext>))
                .ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AddGranitIsolatedDbContext_WithSchemaPerTenant_DoesNotRegisterDefaultSchemaActivator()
    {
        // No default ITenantSchemaActivator since Granit.Persistence.Postgres was
        // decoupled from the generic persistence package. Callers must invoke
        // AddGranitPostgres() (or register their own implementation) before this call.
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"),
            configureSchemaPerTenant: opts => opts.UseInMemoryDatabase("schema-db"));

        services.ShouldNotContain(d => d.ServiceType == typeof(ITenantSchemaActivator));
    }

    [Fact]
    public void AddGranitIsolatedDbContext_WithSchemaPerTenant_RegistersSchemaProvider()
    {
        ServiceCollection services = new();

        services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"),
            configureSchemaPerTenant: opts => opts.UseInMemoryDatabase("schema-db"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITenantSchemaProvider));
    }

    [Fact]
    public void AddGranitIsolatedDbContext_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitIsolatedDbContext<TestDbContext>(
            configureShared: opts => opts.UseInMemoryDatabase("shared"));

        result.ShouldBeSameAs(services);
    }
}
