using System.Threading.Channels;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Granit module for zero-downtime migrations (Expand &amp; Contract pattern).
/// </summary>
/// <remarks>
/// <para>
/// Registers provider-independent services: <see cref="IMigrationCycleRegistry"/>,
/// the default <see cref="ITenantDbIsolator"/> (no-op), and the Channel-based
/// <see cref="IMigrationBatchDispatcher"/>.
/// </para>
/// <para>
/// Install <c>Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine</c> to replace the Channel-based
/// dispatcher with an Outbox-backed <c>IMessageBus</c> implementation.
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

        // Channel-based dispatch (default). Replaced by Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine if installed.
        context.Services.TryAddSingleton(Channel.CreateUnbounded<RunMigrationBatchCommand>());
        context.Services.TryAddSingleton<IMigrationBatchDispatcher, ChannelBatchDispatcher>();
    }
}
