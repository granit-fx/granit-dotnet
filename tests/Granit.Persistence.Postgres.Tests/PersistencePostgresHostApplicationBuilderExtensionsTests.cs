using Granit.Persistence.Hosting;
using Granit.Persistence.Migrations;
using Granit.Persistence.MultiTenancy;
using Granit.Persistence.Postgres.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Postgres.Tests;

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
    public void AddGranitPostgres_TryAdd_DoesNotOverridePreRegistered()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton(NSubstitute.Substitute.For<IGranitMigrationLock>());

        builder.AddGranitPostgres();

        builder.Services.Count(d => d.ServiceType == typeof(IGranitMigrationLock))
            .ShouldBe(1, "TryAddSingleton must not override pre-registered lock");
    }
}
