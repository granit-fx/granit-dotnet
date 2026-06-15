using Granit.AI.AzureOpenAI.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.AI.AzureOpenAI.Extensions;

/// <summary>
/// Health-check registration for the Azure OpenAI provider.
/// </summary>
public static class AzureOpenAIHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds an Azure OpenAI connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Probes <c>GET {Endpoint}/openai/models</c> with the host-level API key, or a Managed Identity
    /// bearer token when no key is set and the fallback is enabled. When neither credential is
    /// available the check reports healthy (per-tenant credentials are not probed). The result
    /// message never leaks the endpoint, key, or token.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"azure-openai"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    public static IHealthChecksBuilder AddGranitAzureOpenAIHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-openai",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpClient(AzureOpenAIHealthCheck.HttpClientName);
        builder.Services.AddSingleton<AzureOpenAIHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<AzureOpenAIHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
