using Granit.Diagnostics.Abstractions;
using Granit.Diagnostics.Internal;
using Granit.Diagnostics.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Diagnostics.Extensions;

/// <summary>
/// Extensions for registering Granit diagnostics services.
/// </summary>
public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit health check infrastructure (endpoint routing, response writer).
    /// Call <c>app.MapGranitHealthChecks()</c> after <c>app.Build()</c> to expose the endpoints.
    /// </summary>
    public static IServiceCollection AddGranitDiagnostics(
        this IServiceCollection services,
        Action<DiagnosticsOptions>? configure = null)
    {
        services.AddHealthChecks();
        services.TryAddSingleton<IHealthCheckAggregator, HealthCheckAggregator>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
