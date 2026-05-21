using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Wolverine.SqlServer.Extensions;
using Granit.Wolverine.SqlServer.Options;

namespace Granit.Wolverine.SqlServer;

/// <summary>
/// Granit module for SQL Server-backed durable Wolverine messaging.
/// </summary>
/// <remarks>
/// Adds a SQL Server Outbox and EF Core transaction integration on top of the
/// provider-agnostic core configured by <see cref="GranitWolverineModule"/>.
/// <para>
/// Reads <see cref="WolverineSqlServerOptions"/> from the <c>"Wolverine:SqlServer"</c>
/// section in <c>appsettings.json</c>. The connection string is validated at startup.
/// </para>
/// <para>
/// For EF Core transaction integration, the consuming service must register its
/// <c>DbContext</c> via <c>services.AddDbContextWithWolverineIntegration&lt;TContext&gt;()</c>
/// instead of the standard <c>services.AddDbContext&lt;TContext&gt;()</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitWolverineModule), typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitWolverineSqlServerModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitWolverineWithSqlServer();
}
