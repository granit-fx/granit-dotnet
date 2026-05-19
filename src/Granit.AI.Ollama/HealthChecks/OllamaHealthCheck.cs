using Granit.AI.Ollama.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.AI.Ollama.HealthChecks;

/// <summary>
/// Health check that verifies Ollama server connectivity by issuing a
/// <c>GET /api/tags</c> request on the configured endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Server reachable → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Server unreachable → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes endpoint URLs or internal details.
/// </remarks>
internal sealed class OllamaHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaProviderOptions> options) : IHealthCheck
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient httpClient = httpClientFactory.CreateClient("GranitAIOllamaHealthCheck");
            httpClient.BaseAddress = new Uri(options.Value.Endpoint);
            httpClient.Timeout = HealthCheckTimeout;

            HttpResponseMessage response = await httpClient
                .GetAsync("/api/tags", cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Ollama server returned non-success status");
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy("Ollama server timed out");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy("Ollama server unreachable");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException)
        {
            // Sanitize: never expose endpoint URLs or internal details in the message
            return HealthCheckResult.Unhealthy($"Ollama health check failed: {ex.GetType().Name}");
        }
    }
}
