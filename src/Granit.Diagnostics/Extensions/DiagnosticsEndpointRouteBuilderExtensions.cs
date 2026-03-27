using Granit.Diagnostics.Options;
using Granit.Diagnostics.ResponseWriters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Diagnostics.Extensions;

/// <summary>
/// Extensions for mapping Granit health check endpoints.
/// </summary>
public static class DiagnosticsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the three Kubernetes health check endpoints (liveness, readiness, startup).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><b>Liveness</b> (<c>/health/live</c>): always returns 200 — never checks dependencies.
    ///   If this probe fails, Kubernetes restarts the pod.</item>
    ///   <item><b>Readiness</b> (<c>/health/ready</c>): checks all dependencies tagged <c>"readiness"</c>.
    ///   Returns 503 on Unhealthy (removes pod from load balancer), 200 on Healthy or Degraded.</item>
    ///   <item><b>Startup</b> (<c>/health/startup</c>): checks dependencies tagged <c>"startup"</c>.
    ///   Disables liveness and readiness while pending.</item>
    /// </list>
    /// </remarks>
    public static IEndpointRouteBuilder MapGranitHealthChecks(
        this IEndpointRouteBuilder endpoints,
        Action<DiagnosticsOptions>? configure = null)
    {
        DiagnosticsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<DiagnosticsOptions>>()?.Value
            ?? new DiagnosticsOptions();

        configure?.Invoke(options);

        // Liveness: the process is alive — no dependency checks, always returns 200
        // AllowAnonymous: Kubernetes kubelet cannot authenticate; must bypass any fallback policy.
        endpoints.MapHealthChecks(options.LivenessPath, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = GranitHealthCheckWriter.WriteMinimalAsync
        }).AllowAnonymous();

        // Readiness: the application can serve traffic
        // Degraded → 200 (pod stays in load balancer; non-critical degradation)
        // Unhealthy → 503 (pod removed from load balancer until dependency recovers)
        // AllowAnonymous: same rationale as liveness.
        endpoints.MapHealthChecks(options.ReadinessPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("readiness"),
            ResponseWriter = GranitHealthCheckWriter.WriteMinimalAsync,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503
            }
        }).AllowAnonymous();

        // Startup: slow initialization guard — disables liveness/readiness while pending
        // AllowAnonymous: same rationale as liveness.
        endpoints.MapHealthChecks(options.StartupPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = GranitHealthCheckWriter.WriteMinimalAsync,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503
            }
        }).AllowAnonymous();

        return endpoints;
    }
}
