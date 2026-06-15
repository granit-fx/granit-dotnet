using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Tika.HealthChecks;

/// <summary>
/// Readiness health check that verifies the Apache Tika sidecar is reachable by issuing a
/// lightweight <c>GET {Uri}version</c> over the same secured <c>granit-tika</c>
/// <see cref="HttpClient"/> (and therefore the same host-wired mTLS handler) the extractor uses.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>2xx → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Non-success status, transport failure, or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The result message never exposes the sidecar URI, host, port, or any header (ISO 27001 / GDPR).
/// </remarks>
internal sealed class TikaSidecarHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<TikaSidecarOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProbeAsync(cancellationToken)
                .WaitAsync(s_timeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Sanitised: never leak the sidecar URI / host / port into the probe surface.
            return HealthCheckResult.Unhealthy($"Tika sidecar unreachable: {ex.GetType().Name}");
        }
    }

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        TikaSidecarOptions tika = options.Value;

        using HttpClient client = httpClientFactory.CreateClient(TikaSidecarTextExtractor.HttpClientName);
        client.Timeout = s_timeout;

        // Tika's /version endpoint returns the banner ("Apache Tika x.y.z") with 200 — the
        // cheapest reachability probe that still proves the parser farm is up, not just the port.
        Uri endpoint = new(tika.Uri, "version");
        using HttpResponseMessage response = await client
            .GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }
}
