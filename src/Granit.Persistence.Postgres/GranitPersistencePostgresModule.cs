using Granit.Core.Modularity;
using Granit.Persistence.Hosting;
using Granit.Persistence.Postgres.Extensions;

namespace Granit.Persistence.Postgres;

/// <summary>
/// Granit module for PostgreSQL-specific persistence extensions.
/// </summary>
/// <remarks>
/// Registers <c>pg_try_advisory_lock</c>-based distributed migration locking.
/// Depend on this module (via <c>[DependsOn]</c>) in any host application that uses
/// PostgreSQL and wants automatic distributed locking during <c>--migrate</c> runs.
/// </remarks>
[DependsOn(typeof(GranitPersistenceHostingModule))]
public sealed class GranitPersistencePostgresModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitPostgres();
}
