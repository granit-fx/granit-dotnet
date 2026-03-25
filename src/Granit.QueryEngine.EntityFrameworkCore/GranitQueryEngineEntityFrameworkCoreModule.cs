using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Persistence;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.QueryEngine.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core persistence layer of <c>Granit.QueryEngine</c>.
/// Provides <c>QueryEngineDbContext</c>, <c>IQueryEngine&lt;T&gt;</c>,
/// and <c>EfCoreSavedViewStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitQueryEngineModule))]
public sealed class GranitQueryEngineEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<QueryEngineEfCoreMetrics>();
        GranitActivitySourceRegistry.Register(QueryEngineEfCoreActivitySource.Name);
    }
}
