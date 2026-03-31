using Granit.Diagnostics.HealthChecks;
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
    IOptions<EntraIdAdminOptions> options) : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "Entra ID";

    protected override string HttpClientName => "MicrosoftGraph";

    protected override HttpRequestMessage CreateRequest()
    {
        EntraIdAdminOptions opts = options.Value;

        return new HttpRequestMessage(HttpMethod.Post, opts.GetTokenEndpoint())
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", opts.ClientId),
                new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
            ]),
        };
    }
}
