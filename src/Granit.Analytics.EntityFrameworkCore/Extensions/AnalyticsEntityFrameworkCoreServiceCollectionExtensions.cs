using Granit.Analytics.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the EF Core executor for Granit.Analytics.
/// </summary>
public static class AnalyticsEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the open generic <see cref="MetricExecutor{TEntity, TValue}"/> as scoped.
    /// Combined with the abstractions module's <c>AddGranitAnalytics</c>, this lets any
    /// host resolve <c>IMetricExecutor&lt;TEntity, TValue&gt;</c> for any pair declared
    /// via <c>AddMetricDefinition&lt;TEntity, TValue, TDefinition&gt;</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsEntityFrameworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(typeof(IMetricExecutor<,>), typeof(MetricExecutor<,>));

        return services;
    }
}
