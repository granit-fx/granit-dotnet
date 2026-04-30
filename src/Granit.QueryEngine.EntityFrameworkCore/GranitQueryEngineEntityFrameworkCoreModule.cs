using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.QueryEngine.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core runtime of <c>Granit.QueryEngine</c>.
/// Registers the open-generic <see cref="IQueryEngine{TEntity}"/> implementation
/// that hosts use to execute queries against any <see cref="IQueryable{T}"/> source.
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
