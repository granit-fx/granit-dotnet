using Granit.Analytics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.Extensions;

/// <summary>
/// DI helpers that operate on the declarative analytics contracts. Lives in
/// <c>Granit.Analytics.Abstractions</c> so modules can register metric definitions
/// without pulling in the analytics runtime.
/// </summary>
public static class AnalyticsAbstractionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a metric definition for the specified entity and value types.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <typeparam name="TValue">The aggregation result type (<c>int</c>, <c>long</c>, <c>decimal</c>, <c>double</c>).</typeparam>
    /// <typeparam name="TDefinition">The metric definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// The same <typeparamref name="TDefinition"/> instance backs three registrations:
    /// the concrete <typeparamref name="TDefinition"/> service (so callers and the
    /// <c>Granit.Entities</c> integrity check can locate it through DI), the typed
    /// <see cref="MetricDefinition{TEntity, TValue}"/> service, and the non-generic
    /// <see cref="IMetricDefinitionDescriptor"/>. Capturing the instance in the closure
    /// (rather than calling <c>GetRequiredService</c> from the descriptor factory)
    /// keeps multiple metrics over the same <c>(TEntity, TValue)</c> closed generics
    /// distinct — the previous indirection collapsed every descriptor entry to the
    /// last-registered metric.
    /// </remarks>
    public static IServiceCollection AddMetricDefinition<TEntity, TValue, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TValue : struct
        where TDefinition : MetricDefinition<TEntity, TValue>, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        TDefinition definition = new();
        services.AddSingleton<TDefinition>(definition);
        services.AddSingleton<MetricDefinition<TEntity, TValue>>(_ => definition);
        services.AddSingleton<IMetricDefinitionDescriptor>(_ => definition);

        return services;
    }
}
