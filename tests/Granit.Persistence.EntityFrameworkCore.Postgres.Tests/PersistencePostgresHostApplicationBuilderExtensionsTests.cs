using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
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
    public void AddGranitPostgres_TryAdd_DoesNotOverridePreRegistered()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton(NSubstitute.Substitute.For<IGranitMigrationLock>());

        builder.AddGranitPostgres();

        builder.Services.Count(d => d.ServiceType == typeof(IGranitMigrationLock))
            .ShouldBe(1, "TryAddSingleton must not override pre-registered lock");
    }
}
