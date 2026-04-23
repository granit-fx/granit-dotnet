namespace Granit.Identity.Federated.RateLimiting;

/// <summary>
/// Default <see cref="ITokenExchangeRateLimiter"/> that allows every request.
/// Replace with a <c>Granit.RateLimiting</c>-backed implementation to enforce a
/// distributed per-user quota in production.
/// </summary>
public sealed class NullTokenExchangeRateLimiter : ITokenExchangeRateLimiter
{
    /// <inheritdoc />
    public Task<TokenExchangeRateLimitDecision> CheckAsync(string targetUserId, CancellationToken cancellationToken) =>
        Task.FromResult(TokenExchangeRateLimitDecision.Allowed);
}
