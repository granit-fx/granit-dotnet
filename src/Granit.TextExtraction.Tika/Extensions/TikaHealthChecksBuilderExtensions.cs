using Granit.TextExtraction.Tika.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.TextExtraction.Tika.Extensions;

/// <summary>
/// Health-check registration for the Apache Tika sidecar.
/// </summary>
public static class TikaHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a readiness health check that verifies the Apache Tika sidecar is reachable.
    /// Call AFTER <see cref="ServiceCollectionExtensions.AddTikaSidecarExtractor"/> so the
    /// <c>granit-tika</c> <see cref="System.Net.Http.HttpClient"/> (and its host-wired mTLS
    /// handler) is available. Tagged <c>"readiness"</c> only — a Tika outage must hold traffic
    /// at the ingress, never fail liveness and restart the pod.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"tika"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitTikaHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "tika",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<TikaSidecarHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<TikaSidecarHealthCheck>(),
            failureStatus,
            ["readiness"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
