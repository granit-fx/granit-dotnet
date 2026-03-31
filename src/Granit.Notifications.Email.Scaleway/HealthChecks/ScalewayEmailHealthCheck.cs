using Granit.Diagnostics.HealthChecks;

namespace Granit.Notifications.Email.Scaleway.HealthChecks;

/// <summary>
/// Health check that verifies Scaleway Transactional Email API connectivity by calling the
/// <c>GET emails?page_size=1</c> endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>200 OK → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy"/></item>
///   <item>401/403 (secret key invalid) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
///   <item>5xx (Scaleway issue) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes secret keys or account details.
/// </remarks>
internal sealed class ScalewayEmailHealthCheck(IHttpClientFactory httpClientFactory)
    : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "Scaleway";

    protected override string HttpClientName => "Scaleway";

    protected override HttpRequestMessage CreateRequest() => new(HttpMethod.Get, "emails?page_size=1");
}
