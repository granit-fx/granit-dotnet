using Granit.Persistence.EntityFrameworkCore.Hosting.Extensions;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Tests;

public sealed class PersistenceHostingHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddGranitMigrateSupport_BindsOptionsFromPersistenceMigrateSection()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Persistence:Migrate:MaxRetries"] = "7";
        builder.Configuration["Persistence:Migrate:ConnectionStringName"] = "MigrationsDb";
        builder.Configuration["Persistence:Migrate:SeedAfterMigration"] = "false";

        builder.AddGranitMigrateSupport();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        GranitMigrateOptions options = sp.GetRequiredService<IOptions<GranitMigrateOptions>>().Value;

        options.MaxRetries.ShouldBe(7);
        options.ConnectionStringName.ShouldBe("MigrationsDb");
        options.SeedAfterMigration.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitMigrateSupport_ConfigureDelegate_WinsOverConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Persistence:Migrate:MaxRetries"] = "7";

        builder.AddGranitMigrateSupport(opts => opts.MaxRetries = 2);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetRequiredService<IOptions<GranitMigrateOptions>>().Value.MaxRetries.ShouldBe(2);
    }

    [Fact]
    public void AddGranitMigrateSupport_InvalidConfiguration_FailsValidation()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Persistence:Migrate:MaxRetries"] = "0";

        builder.AddGranitMigrateSupport();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<GranitMigrateOptions>>().Value);
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
        GranitMigrateOptions options = sp.GetRequiredService<IOptions<GranitMigrateOptions>>().Value;

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
        GranitMigrateOptions options = sp.GetRequiredService<IOptions<GranitMigrateOptions>>().Value;
        options.SeedOnStartup.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitMigrateSupport_RegistersTenantProvisioner()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(ITenantProvisioner));
    }

    [Fact]
    public void AddGranitMigrateSupport_TenantProvisioner_IsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddGranitMigrateSupport();

        ServiceDescriptor descriptor = builder.Services
            .First(d => d.ServiceType == typeof(ITenantProvisioner));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitMigrateSupport_CustomProvisioner_IsNotOverridden()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        // Pre-register a custom provisioner
        builder.Services.AddSingleton<ITenantProvisioner>(
            NSubstitute.Substitute.For<ITenantProvisioner>());

        builder.AddGranitMigrateSupport();

        // TryAddSingleton should not override the custom registration
        builder.Services.Count(d => d.ServiceType == typeof(ITenantProvisioner))
            .ShouldBe(1);
    }
}
