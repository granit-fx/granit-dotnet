using Granit.Persistence.EntityFrameworkCore.Hosting.Internal;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Extensions;

/// <summary>
/// Extension methods for configuring Granit migration support on <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class PersistenceHostingHostApplicationBuilderExtensions
{
    /// <summary>
    /// Enables <c>--migrate</c> CLI support for the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the <see cref="IGranitMigrationRunner"/> and distributed lock infrastructure.
    /// Use <see cref="PersistenceHostingWebApplicationExtensions.HasGranitMigrateFlag"/> and
    /// <see cref="PersistenceHostingWebApplicationExtensions.RunGranitMigrationsAsync"/> after
    /// <c>UseGranitAsync()</c> to trigger migrations when the CLI flag is present.
    /// </para>
    /// <para>
    /// <see cref="GranitMigrateOptions"/> is bound from the
    /// <c>"Persistence:Migrate"</c> configuration section (<see cref="GranitMigrateOptions.SectionName"/>)
    /// with data-annotation validation at startup; the <paramref name="configure"/> delegate
    /// runs after binding, so code-level values win over configuration.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional configuration for migration options (wins over the bound section).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitMigrateSupport(
        this IHostApplicationBuilder builder,
        Action<GranitMigrateOptions>? configure = null)
    {
        builder.Services
            .AddOptions<GranitMigrateOptions>()
            .BindConfiguration(GranitMigrateOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        // Fallback lock only: provider packages (AddGranitPostgres / AddGranitSqlServer)
        // register their distributed lock with AddSingleton (replace), so the real lock
        // wins regardless of whether the provider is registered before or after this call.
        builder.Services.TryAddSingleton<IGranitMigrationLock, NullMigrationLock>();
        builder.Services.TryAddSingleton<IGranitMigrationRunner, GranitMigrationRunner>();
        builder.Services.TryAddSingleton<ITenantProvisioner, AutoTenantProvisioner>();

        // Disable DataSeedingHostedService by default when Hosting is loaded, re-enabled
        // only if SeedOnStartup = true (dev mode). The decision must be made while the
        // ServiceCollection is still mutable, before IOptions materializes — probe the
        // effective value the same way the options pipeline will (section, then delegate).
        GranitMigrateOptions probe = new();
        builder.Configuration.GetSection(GranitMigrateOptions.SectionName).Bind(probe);
        configure?.Invoke(probe);

        if (!probe.SeedOnStartup)
        {
            // Remove any previously registered DataSeedingHostedService
            ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
                d => d.ImplementationType?.Name == "DataSeedingHostedService");

            if (descriptor is not null)
            {
                builder.Services.Remove(descriptor);
            }
        }

        return builder;
    }
}
