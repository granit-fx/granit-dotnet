using Granit.AI.Anthropic.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.HealthChecks;

/// <summary>
/// Readiness health check that verifies the Anthropic API is reachable with the host-level
/// credential by issuing a lightweight <c>GET https://api.anthropic.com/v1/models</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>2xx → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Non-success status, transport failure, or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>No host-level API key configured → <see cref="HealthCheckResult.Healthy"/> (the host may
///   rely solely on per-tenant / per-workspace credentials, out of scope for a host readiness probe)</item>
/// </list>
/// The result message never exposes the API key or any header (ISO 27001 / GDPR).
/// </remarks>
internal sealed class AnthropicHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AnthropicProviderOptions> options) : IHealthCheck
{
    /// <summary>Named <see cref="HttpClient"/> used for the probe.</summary>
    internal const string HttpClientName = "GranitAIAnthropicHealthCheck";

    private const string ModelsEndpoint = "https://api.anthropic.com/v1/models";
    private const string ApiVersion = "2023-06-01";
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AnthropicProviderOptions current = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(current.ApiKey))
        {
            return HealthCheckResult.Healthy(
                "No host-level Anthropic API key configured; per-tenant credentials are not probed.");
        }

        try
        {
            await ProbeAsync(current, cancellationToken)
                .WaitAsync(s_timeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitised: never leak the credential into the probe surface.
            return HealthCheckResult.Unhealthy($"Anthropic API unreachable: {ex.GetType().Name}");
        }
    }

    private async Task ProbeAsync(AnthropicProviderOptions current, CancellationToken cancellationToken)
    {
        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = s_timeout;

        using HttpRequestMessage request = new(HttpMethod.Get, ModelsEndpoint);
        request.Headers.Add("x-api-key", current.ApiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
