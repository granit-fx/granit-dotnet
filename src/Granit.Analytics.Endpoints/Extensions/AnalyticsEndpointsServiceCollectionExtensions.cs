using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.Endpoints.Extensions;

/// <summary>
/// DI registration helpers for <c>Granit.Analytics.Endpoints</c>.
/// </summary>
public static class AnalyticsEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the metric endpoint service plus a non-generic <see cref="IMetricRunner"/>
    /// for every <see cref="IMetricDefinitionDescriptor"/> already registered in the
    /// container. Call AFTER all <c>AddMetricDefinition&lt;TEntity, TValue, TDef&gt;()</c>
    /// have been registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<PeriodResolver>();
        services.TryAddScoped<MetricEndpointService>();

        // Build a closed-generic MetricRunner<TEntity, TValue> for every registered
        // descriptor. Done at registration time so request-time dispatch is reflection-free.
        foreach (ServiceDescriptor descriptor in services
            .Where(d => d.ServiceType == typeof(IMetricDefinitionDescriptor))
            .ToList())
        {
            // The descriptor's implementation factory yields the concrete MetricDefinition<,>.
            // We need the closed types (TEntity, TValue) to construct the runner.
            // Resolve once at registration time using a temporary provider.
            services.AddSingleton<IMetricRunner>(sp =>
            {
                var d = (IMetricDefinitionDescriptor)
                    (descriptor.ImplementationFactory?.Invoke(sp)
                     ?? throw new InvalidOperationException(
                         "IMetricDefinitionDescriptor must be registered with an implementation factory."));

                Type runnerType = typeof(MetricRunner<,>).MakeGenericType(d.EntityType, d.ValueType);
                Type definitionType = typeof(MetricDefinition<,>).MakeGenericType(d.EntityType, d.ValueType);

                object definition = sp.GetRequiredService(definitionType);
                object queryableSource = sp.GetRequiredService(
                    typeof(QueryEngine.IQueryableSource<>).MakeGenericType(d.EntityType));
                object executor = sp.GetRequiredService(
                    typeof(EntityFrameworkCore.IMetricExecutor<,>).MakeGenericType(d.EntityType, d.ValueType));

                return (IMetricRunner)Activator.CreateInstance(runnerType, definition, queryableSource, executor)!;
            });
        }

        return services;
    }
}
