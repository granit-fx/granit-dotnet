using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Persistence.EntityFrameworkCore.Hosting.Internal;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
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
    /// Registers the <see cref="IGranitMigrationRunner"/> and distributed lock infrastructure.
    /// Use <see cref="PersistenceHostingWebApplicationExtensions.HasGranitMigrateFlag"/> and
    /// <see cref="PersistenceHostingWebApplicationExtensions.RunGranitMigrationsAsync"/> after
    /// <c>UseGranitAsync()</c> to trigger migrations when the CLI flag is present.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional configuration for migration options.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitMigrateSupport(
        this IHostApplicationBuilder builder,
        Action<GranitMigrateOptions>? configure = null)
    {
        GranitMigrateOptions options = new();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IGranitMigrationLock, NullMigrationLock>();
        builder.Services.TryAddSingleton<IGranitMigrationRunner, GranitMigrationRunner>();
        builder.Services.TryAddSingleton<ITenantProvisioner, AutoTenantProvisioner>();

        // Disable DataSeedingHostedService by default when Hosting is loaded.
        // Re-enabled only if SeedOnStartup = true (dev mode).
        if (!options.SeedOnStartup)
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
