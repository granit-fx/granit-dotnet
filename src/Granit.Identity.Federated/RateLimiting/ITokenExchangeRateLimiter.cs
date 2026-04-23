namespace Granit.Identity.Federated.RateLimiting;

/// <summary>
/// Gates per-user invocations of the OAuth 2.0 token-exchange flow (RFC 8693). The
/// federated identity layer consults this limiter before minting a user-scoped token
/// to mitigate denial-of-wallet and credential abuse.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation (<see cref="NullTokenExchangeRateLimiter"/>) always
/// allows. Hosts that have <c>Granit.RateLimiting</c> wired up should replace it with
/// an implementation backed by <c>IRateLimitCounterStore</c> so the quota is enforced
/// across all pods — the in-memory variant only protects a single process, so an
/// attacker can cycle through pods to multiply the effective quota.
/// </para>
/// </remarks>
public interface ITokenExchangeRateLimiter
{
    /// <summary>
    /// Checks whether a token exchange for <paramref name="targetUserId"/> is allowed
    /// in the current window and consumes one quota slot if it is.
    /// </summary>
    /// <param name="targetUserId">The subject the new token would impersonate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decision indicating whether the call is permitted.</returns>
    Task<TokenExchangeRateLimitDecision> CheckAsync(string targetUserId, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an <see cref="ITokenExchangeRateLimiter.CheckAsync"/> call.
/// </summary>
/// <param name="IsAllowed"><c>true</c> if the request may proceed; <c>false</c> otherwise.</param>
/// <param name="RetryAfter">When a denial occurs, the minimum interval the caller should wait
/// before retrying. <see cref="TimeSpan.Zero"/> when allowed.</param>
public readonly record struct TokenExchangeRateLimitDecision(bool IsAllowed, TimeSpan RetryAfter)
{
    /// <summary>Convenience for the always-allowed case.</summary>
    public static TokenExchangeRateLimitDecision Allowed => new(true, TimeSpan.Zero);

    /// <summary>Convenience for the denied case with a retry hint.</summary>
    public static TokenExchangeRateLimitDecision Denied(TimeSpan retryAfter) => new(false, retryAfter);
}
