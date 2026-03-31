using System.Net.Http.Headers;
using System.Text;
using Granit.Diagnostics.HealthChecks;
using Granit.Notifications.Zulip.Options;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.Zulip.HealthChecks;

/// <summary>
/// Health check that verifies Zulip Bot API connectivity by calling
/// <c>GET /api/v1/users/me</c> with Basic authentication.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>200 OK → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy"/></item>
///   <item>401/403 (credentials invalid) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
///   <item>5xx (Zulip server issue) → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes API keys, bot emails, or server URLs.
/// </remarks>
internal sealed class ZulipHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<ZulipBotOptions> options) : HttpServiceHealthCheckBase(httpClientFactory)
{
    protected override string ServiceName => "Zulip";

    protected override string HttpClientName => "ZulipBot";

    protected override HttpRequestMessage CreateRequest()
    {
        ZulipBotOptions opts = options.Value;
        string credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{opts.BotEmail}:{opts.ApiKey}"));

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }
}
