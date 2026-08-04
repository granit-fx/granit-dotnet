using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Granit module for zero-downtime migrations (Expand &amp; Contract pattern).
/// </summary>
/// <remarks>
/// <para>
/// Registers provider-independent services: <see cref="IMigrationCycleRegistry"/>,
/// the default <see cref="ITenantDbIsolator"/> and <see cref="ITenantEnumerator"/> (no-ops).
/// </para>
/// <para>
/// The first migration batch per cycle is dispatched via <c>Granit.Commands.ICommandSender</c>
/// (from the startup hosted service), which must be provided by a messaging module (typically
/// <c>Granit.Wolverine</c>). The handler <c>RunMigrationBatchHandler</c> then cascades each
/// subsequent batch as a Wolverine return-value message, enrolled in the same handler outbox.
/// </para>
/// <para>
/// <see cref="MigrationProgressDbContext"/> requires a provider-specific connection string
/// and must be configured separately by calling
/// <c>AddGranitPersistenceMigrations(opts =&gt; opts.UseNpgsql(...))</c> in the application
/// startup code.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed partial class GranitPersistenceEntityFrameworkCoreMigrationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IMigrationCycleRegistry, MigrationCycleRegistry>();
        context.Services.TryAddSingleton<ITenantDbIsolator, NullTenantDbIsolator>();
        context.Services.TryAddSingleton<ITenantEnumerator, NullTenantEnumerator>();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Warns when the resolved <see cref="IGranitMigrationLock"/> is the no-op
    /// <see cref="NullMigrationLock"/> outside the Development environment: with N replicas,
    /// migrations and startup cycle resumes would run concurrently without any distributed
    /// coordination. Register a provider lock via <c>AddGranitPostgres()</c> /
    /// <c>AddGranitSqlServer()</c>. Mirrors the <c>SeedOnStartup</c> warning in the
    /// Hosting module.
    /// </remarks>
    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        if (context.ServiceProvider.GetService<IGranitMigrationLock>() is NullMigrationLock
            && context.ServiceProvider.GetService<IHostEnvironment>()?.IsDevelopment() != true)
        {
            ILogger<GranitPersistenceEntityFrameworkCoreMigrationsModule>? logger =
                context.ServiceProvider.GetService<ILogger<GranitPersistenceEntityFrameworkCoreMigrationsModule>>();

            if (logger is not null)
            {
                LogNullMigrationLockWarning(logger);
            }
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No distributed migration lock is registered (NullMigrationLock resolved) " +
                  "outside the Development environment. With multiple replicas, migrations and " +
                  "startup cycle resumes will run concurrently and unguarded. " +
                  "Register a provider lock via AddGranitPostgres() or AddGranitSqlServer().")]
    private static partial void LogNullMigrationLockWarning(ILogger logger);
}
