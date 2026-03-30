using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer;

/// <summary>
/// Granit module for SQL Server-specific persistence extensions.
/// </summary>
/// <remarks>
/// Registers <c>sp_getapplock</c>-based distributed migration locking.
/// Depend on this module (via <c>[DependsOn]</c>) in any host application that uses
/// SQL Server and wants automatic distributed locking during <c>--migrate</c> runs.
/// </remarks>
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreHostingModule))]
public sealed class GranitPersistenceEntityFrameworkCoreSqlServerModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitSqlServer();
}
