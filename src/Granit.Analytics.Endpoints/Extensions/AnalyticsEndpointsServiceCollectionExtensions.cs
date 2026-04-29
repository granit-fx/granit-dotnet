using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.Endpoints.Extensions;

/// <summary>
/// DI registration helpers for <c>Granit.Analytics.Endpoints</c>.
/// </summary>
public static class AnalyticsEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP-bound metric endpoint orchestrator (<see cref="MetricEndpointService"/>)
    /// and binds <see cref="AnalyticsEndpointsOptions"/> from configuration.
    /// </summary>
    /// <remarks>
    /// Auto-called by <c>GranitAnalyticsEndpointsModule.ConfigureServices</c>. The
    /// per-metric <c>IMetricRunner</c> registry is built later by
    /// <c>AddGranitAnalyticsRunners()</c> (in <c>Granit.Analytics.EntityFrameworkCore</c>),
    /// which the host calls AFTER all <c>AddMetricDefinition&lt;,,&gt;()</c> calls.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AnalyticsEndpointsOptions>()
            .BindConfiguration(AnalyticsEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddScoped<MetricEndpointService>();

        return services;
    }
}
