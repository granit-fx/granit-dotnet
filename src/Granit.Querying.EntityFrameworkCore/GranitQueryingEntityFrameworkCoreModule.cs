using Granit.Core.Diagnostics;
using Granit.Core.Modularity;
using Granit.Persistence;
using Granit.Querying.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Querying.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core persistence layer of <c>Granit.Querying</c>.
/// Provides <c>QueryingDbContext</c>, <c>IQueryEngine&lt;T&gt;</c>,
/// and <c>EfCoreSavedViewStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitQueryingModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitQueryingEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<QueryingEfCoreMetrics>();
        GranitActivitySourceRegistry.Register(QueryingEfCoreActivitySource.Name);
    }
}
