using Granit.Modularity;
using Granit.Persistence.Migrations;
using Granit.Persistence.Migrations.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Persistence.Migrations.Wolverine;

/// <summary>
/// Granit module that replaces the default Channel-based dispatcher with an
/// Outbox-backed <c>IMessageBus</c> implementation for durable migration batch dispatch.
/// </summary>
/// <remarks>
/// <para>
/// This module depends on <see cref="GranitPersistenceMigrationsModule"/> (core migration services)
/// and <see cref="GranitWolverineModule"/> (Wolverine infrastructure).
/// </para>
/// <para>
/// When loaded, it replaces <see cref="IMigrationBatchDispatcher"/> with
/// <see cref="WolverineMigrationBatchDispatcher"/> and registers
/// <see cref="RunMigrationBatchHandler"/> as a Wolverine handler.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceMigrationsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitPersistenceMigrationsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace the default Channel-based dispatcher with Wolverine's IMessageBus.
        var descriptor = ServiceDescriptor
            .Singleton<IMigrationBatchDispatcher, WolverineMigrationBatchDispatcher>();
        context.Services.Replace(descriptor);
    }
}
