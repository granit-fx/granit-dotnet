using Granit.Core.Modularity;
using Granit.Persistence.Hosting;
using Granit.Persistence.SqlServer.Extensions;

namespace Granit.Persistence.SqlServer;

/// <summary>
/// Granit module for SQL Server-specific persistence extensions.
/// </summary>
/// <remarks>
/// Registers <c>sp_getapplock</c>-based distributed migration locking.
/// Depend on this module (via <c>[DependsOn]</c>) in any host application that uses
/// SQL Server and wants automatic distributed locking during <c>--migrate</c> runs.
/// </remarks>
[DependsOn(typeof(GranitPersistenceHostingModule))]
public sealed class GranitPersistenceSqlServerModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitSqlServer();
}
