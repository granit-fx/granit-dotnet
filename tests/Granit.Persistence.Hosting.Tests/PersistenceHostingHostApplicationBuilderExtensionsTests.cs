using Granit.Persistence.Hosting.Extensions;
using Granit.Persistence.Hosting.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Hosting.Tests;

public sealed class PersistenceHostingHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddGranitMigrateSupport_RegistersMigrateOptions()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(GranitMigrateOptions));
    }

    [Fact]
    public void AddGranitMigrateSupport_RegistersMigrationLock()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IGranitMigrationLock));
    }

    [Fact]
    public void AddGranitMigrateSupport_RegistersMigrationRunner()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IGranitMigrationRunner));
    }

    [Fact]
    public void AddGranitMigrateSupport_WithConfigure_AppliesOptions()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport(opts =>
        {
            opts.CliFlag = "--custom-migrate";
            opts.MaxRetries = 5;
        });

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        GranitMigrateOptions options = sp.GetRequiredService<GranitMigrateOptions>();

        options.CliFlag.ShouldBe("--custom-migrate");
        options.MaxRetries.ShouldBe(5);
    }

    [Fact]
    public void AddGranitMigrateSupport_ReturnsBuilderForChaining()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        IHostApplicationBuilder result = builder.AddGranitMigrateSupport();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitMigrateSupport_DefaultOptions_SeedOnStartupFalse()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        GranitMigrateOptions options = sp.GetRequiredService<GranitMigrateOptions>();
        options.SeedOnStartup.ShouldBeFalse();
    }
}
