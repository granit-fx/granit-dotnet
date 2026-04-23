using FirebaseAdmin.Auth;
using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.GoogleCloud.HealthChecks;

/// <summary>
/// Health check that verifies Google Cloud Identity Platform connectivity by issuing a
/// minimal <see cref="IFirebaseAuthTransport.PingAsync"/> call (reads the first page of
/// users with <c>PageSize = 1</c>).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Ping succeeds (any response, even an empty page) → <see cref="HealthCheckResult.Healthy"/></item>
///   <item><see cref="FirebaseAuthException"/> → <see cref="HealthCheckResult.Unhealthy"/> (auth or service error)</item>
///   <item>Other exception (network, timeout, misconfiguration) → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes credentials, project IDs, or stack traces.
/// </remarks>
internal sealed class GoogleCloudIdentityHealthCheck(
    IFirebaseAuthTransport transport,
    IOptions<GoogleCloudIdentityOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ProjectId))
        {
            return HealthCheckResult.Unhealthy("Firebase Auth ProjectId is not configured.");
        }

        try
        {
            await transport.PingAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (FirebaseAuthException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Firebase Auth probe failed: {ex.AuthErrorCode}");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose credentials, project IDs, or stack traces.
            return HealthCheckResult.Unhealthy(
                $"Firebase Auth probe failed: {ex.GetType().Name}");
        }
    }
}
