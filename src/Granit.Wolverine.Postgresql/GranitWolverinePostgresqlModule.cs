using Granit.Modularity;
using Granit.Persistence;
using Granit.Wolverine.Postgresql.Extensions;
using Granit.Wolverine.Postgresql.Options;

namespace Granit.Wolverine.Postgresql;

/// <summary>
/// Granit module for PostgreSQL-backed durable Wolverine messaging.
/// </summary>
/// <remarks>
/// Adds a PostgreSQL Outbox and EF Core transaction integration on top of the
/// provider-agnostic core configured by <see cref="GranitWolverineModule"/>.
/// <para>
/// Reads <see cref="WolverinePostgresqlOptions"/> from the <c>"WolverinePostgresql"</c>
/// section in <c>appsettings.json</c>. The connection string is validated at startup.
/// </para>
/// <para>
/// For EF Core transaction integration, the consuming service must register its
/// <c>DbContext</c> via <c>services.AddDbContextWithWolverineIntegration&lt;TContext&gt;()</c>
/// instead of the standard <c>services.AddDbContext&lt;TContext&gt;()</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitWolverineModule), typeof(GranitPersistenceModule))]
public sealed class GranitWolverinePostgresqlModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitWolverineWithPostgresql();
}
