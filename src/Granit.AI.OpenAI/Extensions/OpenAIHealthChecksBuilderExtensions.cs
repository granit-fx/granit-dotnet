using Granit.AI.OpenAI.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.AI.OpenAI.Extensions;

/// <summary>
/// Health-check registration for the OpenAI provider.
/// </summary>
public static class OpenAIHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds an OpenAI connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Probes <c>GET {Endpoint}/models</c> with the host-level API key. When no host-level key is
    /// configured the check reports healthy (per-tenant credentials are not probed). The result
    /// message never leaks the endpoint or credential.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"openai"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitOpenAIHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "openai",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpClient(OpenAIHealthCheck.HttpClientName);
        builder.Services.AddSingleton<OpenAIHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<OpenAIHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
