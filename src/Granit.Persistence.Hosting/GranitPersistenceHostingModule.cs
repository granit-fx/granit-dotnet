using Granit.Core.Modularity;
using Granit.Persistence.Hosting.Options;
using Granit.Persistence.Migrations;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.Hosting;

/// <summary>
/// Granit module for EF Core migration hosting infrastructure.
/// Provides <c>--migrate</c> CLI support, distributed locking, and automatic
/// <see cref="IMigratableModule{TContext}"/> discovery from the module dependency graph.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitPersistenceMigrationsModule))]
public sealed partial class GranitPersistenceHostingModule : GranitModule
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
            var logger = (ILogger<GranitPersistenceHostingModule>)
                context.ServiceProvider.GetService(typeof(ILogger<GranitPersistenceHostingModule>))!;

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
