using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Granit.AI.AzureOpenAI.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.HealthChecks;

/// <summary>
/// Readiness health check that verifies the Azure OpenAI resource is reachable with the
/// host-level credential by issuing a lightweight <c>GET {Endpoint}/openai/models</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>API key configured → probes with the <c>api-key</c> header.</item>
///   <item>No API key + <see cref="AzureOpenAIProviderOptions.AllowManagedIdentityFallback"/> →
///   acquires an AAD token via <see cref="DefaultAzureCredential"/> for the Cognitive Services
///   scope and probes with a bearer token.</item>
///   <item>No API key and Managed Identity fallback disabled → <see cref="HealthCheckResult.Healthy"/>
///   (the host may rely solely on per-tenant credentials, out of scope for a host readiness probe).</item>
/// </list>
/// 2xx → Healthy; non-success status, transport failure, or timeout → Unhealthy. The result
/// message never exposes the endpoint, key, or token (ISO 27001 / GDPR).
/// </remarks>
internal sealed class AzureOpenAIHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AzureOpenAIProviderOptions> options) : IHealthCheck
{
    /// <summary>Named <see cref="HttpClient"/> used for the probe.</summary>
    internal const string HttpClientName = "GranitAIAzureOpenAIHealthCheck";

    // Stable GA data-plane API version exposing the resource's model list.
    private const string ApiVersion = "2024-10-21";

    // AAD scope for the Azure OpenAI (Cognitive Services) data plane.
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AzureOpenAIProviderOptions current = options.CurrentValue;

        bool hasApiKey = !string.IsNullOrWhiteSpace(current.ApiKey);
        if (!hasApiKey && !current.AllowManagedIdentityFallback)
        {
            return HealthCheckResult.Healthy(
                "No host-level Azure OpenAI credential configured (API key empty, Managed Identity " +
                "fallback disabled); per-tenant credentials are not probed.");
        }

        try
        {
            await ProbeAsync(current, hasApiKey, cancellationToken)
                .WaitAsync(s_timeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitised: never leak the endpoint, key, or token into the probe surface.
            return HealthCheckResult.Unhealthy($"Azure OpenAI unreachable: {ex.GetType().Name}");
        }
    }

    private async Task ProbeAsync(
        AzureOpenAIProviderOptions current, bool hasApiKey, CancellationToken cancellationToken)
    {
        string baseUrl = current.Endpoint.TrimEnd('/');

        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = s_timeout;

        using HttpRequestMessage request = new(
            HttpMethod.Get, $"{baseUrl}/openai/models?api-version={ApiVersion}");

        if (hasApiKey)
        {
            request.Headers.Add("api-key", current.ApiKey);
        }
        else
        {
            // Managed Identity fallback: the SDK uses DefaultAzureCredential the same way, so the
            // probe mirrors the live trust principal. Token acquisition is bounded by s_timeout.
            AccessToken token = await new DefaultAzureCredential()
                .GetTokenAsync(new TokenRequestContext([CognitiveServicesScope]), cancellationToken)
                .ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        using HttpResponseMessage response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
