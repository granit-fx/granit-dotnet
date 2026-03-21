using Granit.Persistence.Hosting;
using Granit.Persistence.SqlServer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.SqlServer.Tests;

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
    public void AddGranitSqlServer_TryAdd_DoesNotOverridePreRegistered()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton(NSubstitute.Substitute.For<IGranitMigrationLock>());

        builder.AddGranitSqlServer();

        builder.Services.Count(d => d.ServiceType == typeof(IGranitMigrationLock))
            .ShouldBe(1, "TryAddSingleton must not override pre-registered lock");
    }
}
