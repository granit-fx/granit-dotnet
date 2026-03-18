using Granit.Core.Modularity;
using Granit.Persistence.Hosting;
using Granit.Persistence.Postgres.Extensions;

namespace Granit.Persistence.Postgres;

/// <summary>
/// Granit module for PostgreSQL-specific persistence extensions.
/// </summary>
/// <remarks>
/// Registers:
/// <list type="bullet">
///   <item><c>pg_try_advisory_lock</c>-based distributed migration lock (<see cref="Hosting.IGranitMigrationLock"/>).</item>
///   <item>PostgreSQL schema activator (<see cref="Granit.Persistence.MultiTenancy.ITenantSchemaActivator"/>) for <c>SET search_path</c>.</item>
///   <item>PostgreSQL tenant DB isolator (<see cref="Granit.Persistence.Migrations.ITenantDbIsolator"/>) for schema-per-tenant migrations.</item>
/// </list>
/// Depend on this module (via <c>[DependsOn]</c>) in any host application that uses PostgreSQL.
/// Must be declared before <c>GranitPersistenceHostingModule</c> in the dependency graph so
/// <c>TryAdd</c> registrations win over the no-op fallbacks.
/// </remarks>
[DependsOn(typeof(GranitPersistenceHostingModule))]
public sealed class GranitPersistencePostgresModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitPostgres();
}
