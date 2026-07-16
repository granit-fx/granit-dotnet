using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Granit.Identity.Federated.Cognito.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Cognito.HealthChecks;

/// <summary>
/// Health check that verifies AWS Cognito connectivity by issuing a minimal
/// <c>ListUsers</c> (limit 1) against the configured user pool — the same permission the
/// provider itself needs, so a green check means the service account can actually read.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Pool reachable and readable → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Credentials rejected (<c>NotAuthorized</c>) or pool missing → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>Other AWS service error → <see cref="HealthCheckResult.Degraded"/></item>
///   <item>Unreachable or timeout → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes credentials, user data, or the pool id.
/// </remarks>
internal sealed class CognitoHealthCheck(
    IAmazonCognitoIdentityProvider cognitoClient,
    IOptions<CognitoAdminOptions> options) : IHealthCheck
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await cognitoClient
                .ListUsersAsync(
                    new ListUsersRequest { UserPoolId = options.Value.UserPoolId, Limit = 1 },
                    cancellationToken)
                .WaitAsync(HealthCheckTimeout, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy("AWS Cognito user pool is reachable.");
        }
        catch (NotAuthorizedException)
        {
            return HealthCheckResult.Unhealthy("AWS Cognito rejected the service-account credentials.");
        }
        catch (ResourceNotFoundException)
        {
            return HealthCheckResult.Unhealthy("AWS Cognito user pool was not found.");
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Unhealthy("AWS Cognito did not respond within the health-check timeout.");
        }
        catch (AmazonServiceException ex)
        {
            // Sanitize: never expose the underlying message.
            return HealthCheckResult.Degraded($"AWS Cognito returned a service error: {ex.GetType().Name}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"AWS Cognito is unreachable: {ex.GetType().Name}");
        }
    }
}
