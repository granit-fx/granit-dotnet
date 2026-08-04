using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class PersistenceTenantExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    // -------------------------------------------------------------------------
    // AddGranitTenantPerDatabaseDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitTenantPerDatabaseDbContext_RegistersFactory_Scoped()
    {
        ServiceCollection services = new();

        services.AddGranitTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<TestDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitTenantPerDatabaseDbContext_RegistersContext_Scoped()
    {
        ServiceCollection services = new();

        services.AddGranitTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.ShouldContain(d =>
            d.ServiceType == typeof(TestDbContext) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitTenantPerDatabaseDbContext_TryAdd_DoesNotOverridePreRegistered()
    {
        ServiceCollection services = new();
        services.AddScoped<IDbContextFactory<TestDbContext>>(
            _ => null!); // pre-register
        services.AddGranitTenantPerDatabaseDbContext<TestDbContext>((opts, cs) =>
            opts.UseInMemoryDatabase(cs));

        services.Count(d => d.ServiceType == typeof(IDbContextFactory<TestDbContext>))
                .ShouldBe(1, "TryAddScoped must not override pre-registered factory");
    }

    [Fact]
    public void AddGranitTenantPerDatabaseDbContext_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitTenantPerDatabaseDbContext<TestDbContext>(
            (opts, cs) => opts.UseInMemoryDatabase(cs));

        result.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // AddGranitTenantPerSchemaDbContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitTenantPerSchemaDbContext_RegistersFactory_Scoped()
    {
        ServiceCollection services = new();

        services.AddGranitTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(IDbContextFactory<TestDbContext>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitTenantPerSchemaDbContext_RegistersDefaultSchemaProvider()
    {
        ServiceCollection services = new();

        services.AddGranitTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITenantSchemaProvider) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitTenantPerSchemaDbContext_WithSchemaOptions_RegistersOptions()
    {
        ServiceCollection services = new();

        services.AddGranitTenantPerSchemaDbContext<TestDbContext>(
            opts => opts.UseInMemoryDatabase("shared-db"),
            schema => schema.Prefix = "t_");

        services.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddGranitTenantPerSchemaDbContext_DoesNotRegisterDefaultSchemaActivator()
    {
        // No default ITenantSchemaActivator since Granit.Persistence.EntityFrameworkCore.Postgres was
        // decoupled from the generic persistence package. Callers must invoke
        // AddGranitPostgres() (or register their own implementation) before this call.
        ServiceCollection services = new();

        services.AddGranitTenantPerSchemaDbContext<TestDbContext>(opts =>
            opts.UseInMemoryDatabase("shared-db"));

        services.ShouldNotContain(d => d.ServiceType == typeof(ITenantSchemaActivator));
    }

    [Fact]
    public void AddGranitTenantPerSchemaDbContext_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitTenantPerSchemaDbContext<TestDbContext>(
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
        // No default ITenantSchemaActivator since Granit.Persistence.EntityFrameworkCore.Postgres was
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
