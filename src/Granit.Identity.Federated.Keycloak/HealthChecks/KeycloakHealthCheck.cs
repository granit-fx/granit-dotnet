using System.Net;
using Granit.Identity.Federated.Keycloak.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.HealthChecks;

/// <summary>
/// Health check that verifies Keycloak connectivity by requesting a token via the
/// <c>client_credentials</c> grant on the OpenID Connect token endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Token obtained successfully → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>401/403 (credentials invalid) → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>Unreachable or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes client secrets or tokens.
/// </remarks>
internal sealed class KeycloakHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            KeycloakAdminOptions opts = options.Value;
            using HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");

            using FormUrlEncodedContent content = new(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", opts.ClientId),
                new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
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
                ? HealthCheckResult.Unhealthy($"Keycloak auth failed: {(int)response.StatusCode}")
                : HealthCheckResult.Degraded($"Keycloak returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose URLs, secrets, or tokens in the message
            return HealthCheckResult.Unhealthy($"Keycloak unreachable: {ex.GetType().Name}");
        }
    }
}
