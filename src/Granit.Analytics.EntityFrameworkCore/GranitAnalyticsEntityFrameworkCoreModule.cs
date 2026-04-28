using Granit.Analytics.EntityFrameworkCore.Extensions;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.EntityFrameworkCore;

/// <summary>
/// EF Core executor module for Granit.Analytics — registers the open generic
/// <see cref="Internal.MetricExecutor{TEntity, TValue}"/> as scoped, so consumers
/// resolve <c>IMetricExecutor&lt;TEntity, TValue&gt;</c> for any combination of
/// entity and value type they declared a metric for.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitPersistenceEntityFrameworkCoreModule"/> per the
/// framework rule that every <c>*.EntityFrameworkCore</c> package declares the
/// dependency — keeps the EF Core ecosystem coherent even when this package
/// does not own a DbContext (it only consumes one through <c>IQueryEngine</c>).
/// </remarks>
[DependsOn(typeof(GranitAnalyticsModule))]
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAnalyticsEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAnalyticsEntityFrameworkCore();
}
