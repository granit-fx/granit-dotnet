using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Diagnostics.HealthChecks;

/// <summary>
/// Base class for health checks that verify external HTTP service connectivity.
/// Handles the common pattern: send request → check status code → Healthy/Degraded/Unhealthy.
/// </summary>
/// <remarks>
/// <para>
/// Status code mapping:
/// <list type="bullet">
///   <item>2xx → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>401/403 → <see cref="HealthCheckResult.Unhealthy"/> (auth failure, pod should be removed)</item>
///   <item>Other non-2xx → <see cref="HealthCheckResult.Degraded"/> (temporary issue)</item>
///   <item>Exception → <see cref="HealthCheckResult.Unhealthy"/> (unreachable)</item>
/// </list>
/// </para>
/// <para>
/// Security: error messages never expose URLs, credentials, API keys, or tokens —
/// only the service name and exception type name.
/// </para>
/// </remarks>
public abstract class HttpServiceHealthCheckBase(
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Display name for error messages (e.g., "Brevo", "Keycloak").</summary>
    protected abstract string ServiceName { get; }

    /// <summary>Named HTTP client registered via <c>AddHttpClient("name")</c>.</summary>
    protected abstract string HttpClientName { get; }

    /// <summary>
    /// Creates the <see cref="HttpRequestMessage"/> to send.
    /// Override for custom HTTP method, headers, or body (e.g., POST with form data).
    /// Default: <c>GET {endpoint}</c>.
    /// </summary>
    protected abstract HttpRequestMessage CreateRequest();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            using HttpRequestMessage request = CreateRequest();

            using HttpResponseMessage response = await client
                .SendAsync(request, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }

            return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? HealthCheckResult.Unhealthy($"{ServiceName} auth failed: {(int)response.StatusCode}")
                : HealthCheckResult.Degraded($"{ServiceName} returned {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{ServiceName} health check canceled");
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy($"{ServiceName} unreachable: timeout");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{ServiceName} unreachable: {ex.GetType().Name}");
        }
    }
}
