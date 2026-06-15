using Granit.AI.Anthropic.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.AI.Anthropic.Extensions;

/// <summary>
/// Health-check registration for the Anthropic provider.
/// </summary>
public static class AnthropicHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds an Anthropic connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Probes <c>GET https://api.anthropic.com/v1/models</c> with the host-level API key. When no
    /// host-level key is configured the check reports healthy (per-tenant credentials are not
    /// probed). The result message never leaks the credential.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"anthropic"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitAnthropicHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "anthropic",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpClient(AnthropicHealthCheck.HttpClientName);
        builder.Services.AddSingleton<AnthropicHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<AnthropicHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
