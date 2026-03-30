using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Hosting.Options;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Hosting;

/// <summary>
/// Granit module for EF Core migration hosting infrastructure.
/// Provides <c>--migrate</c> CLI support, distributed locking, and automatic
/// <see cref="IMigratableModule{TContext}"/> discovery from the module dependency graph.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed partial class GranitPersistenceEntityFrameworkCoreHostingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Registration is handled by AddGranitMigrateSupport() extension method.
        // This module exists to declare the dependency graph entry point.
    }

    /// <inheritdoc/>
    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var options = context.ServiceProvider.GetService(typeof(GranitMigrateOptions))
            as GranitMigrateOptions;

        if (options?.SeedOnStartup == true)
        {
            var logger = (ILogger<GranitPersistenceEntityFrameworkCoreHostingModule>)
                context.ServiceProvider.GetService(typeof(ILogger<GranitPersistenceEntityFrameworkCoreHostingModule>))!;

            LogSeedOnStartupWarning(logger);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit Data Seeding is running on startup. " +
                  "This is unsafe for multi-instance deployments. " +
                  "Set SeedOnStartup = false and use --migrate instead.")]
    private static partial void LogSeedOnStartupWarning(ILogger logger);
}
