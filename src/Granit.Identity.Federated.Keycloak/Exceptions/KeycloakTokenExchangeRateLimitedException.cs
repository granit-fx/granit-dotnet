namespace Granit.Identity.Federated.Keycloak.Exceptions;

/// <summary>
/// Thrown when <see cref="Internal.KeycloakUserTokenExchangeService.ExchangeTokenForUserAsync"/>
/// is denied by the configured <c>ITokenExchangeRateLimiter</c>. Maps cleanly to a
/// 429 Too Many Requests at the HTTP boundary.
/// </summary>
internal sealed class KeycloakTokenExchangeRateLimitedException : Exception
{
    public KeycloakTokenExchangeRateLimitedException(string targetUserId, TimeSpan retryAfter)
        : base($"Keycloak token exchange for user '{targetUserId}' is rate-limited. Retry after {retryAfter.TotalSeconds:F0}s.")
    {
        TargetUserId = targetUserId;
        RetryAfter = retryAfter;
    }

    public string TargetUserId { get; }

    public TimeSpan RetryAfter { get; }
}
