using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.QueryEngine.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core persistence layer of <c>Granit.QueryEngine</c>.
/// Provides <c>QueryEngineDbContext</c>, <c>IQueryEngine&lt;T&gt;</c>,
/// and <c>EfCoreSavedViewStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitQueryEngineModule))]
public sealed class GranitQueryEngineEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped(typeof(IQueryEngine<>), typeof(QueryEngine<>));
        GranitActivitySourceRegistry.Register(QueryEngineEfCoreActivitySource.Name);
    }
}
