using Granit.Diagnostics.HealthChecks;

namespace Granit.Notifications.Email.SendGrid.HealthChecks;

/// <summary>
/// Health check that verifies SendGrid API connectivity by calling the
/// <c>GET scopes</c> endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>200 OK → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy"/></item>
///   <item>401/403 (API key invalid) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
///   <item>5xx (SendGrid issue) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes API keys or account details.
/// </remarks>
internal sealed class SendGridHealthCheck(IHttpClientFactory httpClientFactory)
    : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "SendGrid";

    protected override string HttpClientName => "SendGrid";

    protected override HttpRequestMessage CreateRequest() => new(HttpMethod.Get, "scopes");
}
