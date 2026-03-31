using Granit.Diagnostics.HealthChecks;
using Granit.Notifications.Twilio.Options;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Twilio.HealthChecks;

/// <summary>
/// Health check that verifies Twilio API connectivity by calling
/// <c>GET /2010-04-01/Accounts/{AccountSid}.json</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>200 OK → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy"/></item>
///   <item>401/403 (credentials invalid) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
///   <item>5xx (Twilio issue) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes credentials or account details.
/// </remarks>
internal sealed class TwilioHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<TwilioOptions> options) : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "Twilio";

    protected override string HttpClientName => "Twilio";

    protected override HttpRequestMessage CreateRequest()
    {
        TwilioOptions opts = options.CurrentValue;
        return new HttpRequestMessage(HttpMethod.Get, $"2010-04-01/Accounts/{opts.AccountSid}.json");
    }
}
