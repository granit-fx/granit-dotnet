using Granit.Modularity;
using Granit.Observability;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;
using Npgsql;

namespace Granit.Persistence.EntityFrameworkCore.Postgres;

/// <summary>
/// Granit module for PostgreSQL-specific persistence extensions.
/// </summary>
/// <remarks>
/// Registers:
/// <list type="bullet">
///   <item><c>pg_try_advisory_lock</c>-based distributed migration lock (<see cref="Migrations.IGranitMigrationLock"/>).</item>
///   <item>PostgreSQL schema activator (<see cref="Granit.Persistence.EntityFrameworkCore.MultiTenancy.ITenantSchemaActivator"/>) for <c>SET search_path</c>.</item>
///   <item>PostgreSQL tenant DB isolator (<see cref="Granit.Persistence.EntityFrameworkCore.Migrations.ITenantDbIsolator"/>) for schema-per-tenant migrations.</item>
/// </list>
/// Depend on this module (via <c>[DependsOn]</c>) in any host application that uses PostgreSQL.
/// Must be declared before <c>GranitPersistenceEntityFrameworkCoreHostingModule</c> in the dependency graph so
/// <c>TryAdd</c> registrations win over the no-op fallbacks.
/// </remarks>
[DependsOn(
    typeof(GranitObservabilityModule),
    typeof(GranitPersistenceEntityFrameworkCoreHostingModule))]
public sealed class GranitPersistenceEntityFrameworkCorePostgresModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Builder.AddGranitPostgres();

        // Auto-wire Npgsql OTel instrumentation (traces + metrics) when
        // Granit.Observability is hosted. Adds the "Npgsql" ActivitySource and the
        // Npgsql Meter to the tracer/meter provider.
        GranitOpenTelemetryRegistry.RegisterTracing(t => t.AddNpgsql());
        GranitOpenTelemetryRegistry.RegisterMetrics(m => m.AddNpgsqlInstrumentation());
    }
}
