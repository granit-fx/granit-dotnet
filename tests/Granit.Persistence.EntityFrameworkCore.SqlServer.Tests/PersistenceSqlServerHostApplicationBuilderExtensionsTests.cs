using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Hosting.Extensions;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Tests;

public sealed class PersistenceSqlServerHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddGranitSqlServer_RegistersMigrationLock()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitSqlServer();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IGranitMigrationLock));
    }

    [Fact]
    public void AddGranitSqlServer_ReturnsBuilderForChaining()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        IHostApplicationBuilder result = builder.AddGranitSqlServer();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitSqlServer_RegistersSqlClientProviderFactory()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitSqlServer();

        DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out DbProviderFactory? factory)
            .ShouldBeTrue("SqlClientFactory must be registered so SqlServerAppLock can resolve it — " +
                          "without it every SQL Server host silently migrates without a distributed lock");
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitSqlServer_AfterMigrateSupport_RealLockWins()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddLogging();

        builder.AddGranitMigrateSupport();
        builder.AddGranitSqlServer();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IGranitMigrationLock>().ShouldBeOfType<SqlServerAppLock>();
    }

    [Fact]
    public void AddGranitSqlServer_BeforeMigrateSupport_RealLockWins()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddLogging();

        builder.AddGranitSqlServer();
        builder.AddGranitMigrateSupport();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IGranitMigrationLock>().ShouldBeOfType<SqlServerAppLock>();
    }
}
