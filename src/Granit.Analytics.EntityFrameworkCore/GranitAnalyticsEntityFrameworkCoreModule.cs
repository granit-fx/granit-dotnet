using Granit.Analytics.EntityFrameworkCore.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.EntityFrameworkCore;

/// <summary>
/// EF Core executor module for Granit.Analytics — registers the open generic
/// <see cref="Internal.MetricExecutor{TEntity, TValue}"/> as scoped, so consumers
/// resolve <c>IMetricExecutor&lt;TEntity, TValue&gt;</c> for any combination of
/// entity and value type they declared a metric for.
/// </summary>
[DependsOn(typeof(GranitAnalyticsModule))]
public sealed class GranitAnalyticsEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAnalyticsEntityFrameworkCore();
}
