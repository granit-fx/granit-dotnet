using System.Net;
using Granit.Identity.Federated.EntraId.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.EntraId.HealthChecks;

/// <summary>
/// Health check that verifies Microsoft Entra ID connectivity by requesting a token
/// via the <c>client_credentials</c> grant on the OAuth 2.0 token endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Token obtained successfully → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>401/403 (credentials invalid) → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>5xx (Azure AD issue) → <see cref="HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes client secrets, tenant IDs, or tokens.
/// </remarks>
internal sealed class EntraIdHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<EntraIdAdminOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EntraIdAdminOptions opts = options.Value;
            using HttpClient client = httpClientFactory.CreateClient("MicrosoftGraph");

            using FormUrlEncodedContent content = new(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", opts.ClientId),
                new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
            ]);

            using HttpResponseMessage response = await client
                .PostAsync(opts.GetTokenEndpoint(), content, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }

            return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? HealthCheckResult.Unhealthy($"Entra ID auth failed: {(int)response.StatusCode}")
                : HealthCheckResult.Degraded($"Entra ID returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose tenant IDs, secrets, or tokens
            return HealthCheckResult.Unhealthy($"Entra ID unreachable: {ex.GetType().Name}");
        }
    }
}
