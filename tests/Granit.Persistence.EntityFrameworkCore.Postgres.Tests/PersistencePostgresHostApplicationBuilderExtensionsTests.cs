using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting.Extensions;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Tests;

public sealed class PersistencePostgresHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddGranitPostgres_RegistersMigrationLock()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitPostgres();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IGranitMigrationLock));
    }

    [Fact]
    public void AddGranitPostgres_RegistersTenantSchemaActivator()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitPostgres();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantSchemaActivator));
    }

    [Fact]
    public void AddGranitPostgres_RegistersTenantDbIsolator()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitPostgres();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantDbIsolator));
    }

    [Fact]
    public void AddGranitPostgres_ReturnsBuilderForChaining()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        IHostApplicationBuilder result = builder.AddGranitPostgres();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitPostgres_RegistersNpgsqlProviderFactory()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitPostgres();

        DbProviderFactories.TryGetFactory("Npgsql", out DbProviderFactory? factory)
            .ShouldBeTrue("NpgsqlFactory must be registered so NpgsqlAdvisoryMigrationLock can resolve it");
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitPostgres_AfterMigrateSupport_RealLockWins()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddLogging();

        builder.AddGranitMigrateSupport();
        builder.AddGranitPostgres();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IGranitMigrationLock>().ShouldBeOfType<NpgsqlAdvisoryMigrationLock>();
    }

    [Fact]
    public void AddGranitPostgres_BeforeMigrateSupport_RealLockWins()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddLogging();

        builder.AddGranitPostgres();
        builder.AddGranitMigrateSupport();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IGranitMigrationLock>().ShouldBeOfType<NpgsqlAdvisoryMigrationLock>();
    }
}
