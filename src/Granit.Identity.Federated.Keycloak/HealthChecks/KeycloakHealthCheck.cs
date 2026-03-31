using Granit.Diagnostics.HealthChecks;
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
    IOptions<KeycloakAdminOptions> options) : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "Keycloak";

    protected override string HttpClientName => "KeycloakAdmin";

    protected override HttpRequestMessage CreateRequest()
    {
        KeycloakAdminOptions opts = options.Value;

        return new HttpRequestMessage(HttpMethod.Post, opts.GetTokenEndpoint())
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", opts.ClientId),
                new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
            ]),
        };
    }
}
