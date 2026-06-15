using System.Net.Http.Headers;
using Granit.AI.OpenAI.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.AI.OpenAI.HealthChecks;

/// <summary>
/// Readiness health check that verifies the OpenAI API is reachable with the host-level
/// credential by issuing a lightweight <c>GET {Endpoint}/models</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>2xx → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Non-success status, transport failure, or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>No host-level API key configured → <see cref="HealthCheckResult.Healthy"/> (the host may
///   rely solely on per-tenant / per-workspace credentials, which are out of scope for a host
///   readiness probe — failing here would wrongly hold traffic on a valid deployment)</item>
/// </list>
/// The result message never exposes the API key, endpoint, or any header (ISO 27001 / GDPR).
/// </remarks>
internal sealed class OpenAIHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenAIProviderOptions> options) : IHealthCheck
{
    /// <summary>Named <see cref="HttpClient"/> used for the probe.</summary>
    internal const string HttpClientName = "GranitAIOpenAIHealthCheck";

    private const string DefaultEndpoint = "https://api.openai.com/v1";
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        OpenAIProviderOptions current = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(current.ApiKey))
        {
            return HealthCheckResult.Healthy(
                "No host-level OpenAI API key configured; per-tenant credentials are not probed.");
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
            // Sanitised: never leak the endpoint or credential into the probe surface.
            return HealthCheckResult.Unhealthy($"OpenAI API unreachable: {ex.GetType().Name}");
        }
    }

    private async Task ProbeAsync(OpenAIProviderOptions current, CancellationToken cancellationToken)
    {
        string baseUrl =
            (string.IsNullOrWhiteSpace(current.Endpoint) ? DefaultEndpoint : current.Endpoint).TrimEnd('/');

        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = s_timeout;

        using HttpRequestMessage request = new(HttpMethod.Get, $"{baseUrl}/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.ApiKey);

        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
