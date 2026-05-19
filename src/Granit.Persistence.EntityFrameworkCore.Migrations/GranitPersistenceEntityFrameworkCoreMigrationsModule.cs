using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
/// Migration batch commands are dispatched via <c>Granit.Commands.ICommandSender</c>,
/// which must be provided by a messaging module (typically <c>Granit.Wolverine</c>).
/// The handler <c>RunMigrationBatchHandler</c> executes a batch and cascades the next command.
/// </para>
/// <para>
/// <see cref="MigrationProgressDbContext"/> requires a provider-specific connection string
/// and must be configured separately by calling
/// <c>AddGranitPersistenceMigrations(opts =&gt; opts.UseNpgsql(...))</c> in the application
/// startup code.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitPersistenceEntityFrameworkCoreMigrationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IMigrationCycleRegistry, MigrationCycleRegistry>();
        context.Services.TryAddSingleton<ITenantDbIsolator, NullTenantDbIsolator>();
        context.Services.TryAddSingleton<ITenantEnumerator, NullTenantEnumerator>();
    }
}
