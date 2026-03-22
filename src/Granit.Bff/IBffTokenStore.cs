namespace Granit.Bff;

/// <summary>
/// Stores and retrieves BFF session tokens in a distributed cache.
/// Tokens are keyed by session ID and have a configurable TTL.
/// </summary>
public interface IBffTokenStore
{
    /// <summary>Stores a token set for the given session.</summary>
    /// <param name="sessionId">The session identifier (from the session cookie).</param>
    /// <param name="tokens">The token set to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreAsync(string sessionId, BffTokenSet tokens, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the token set for the given session, or <c>null</c> if expired or missing.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BffTokenSet?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes the token set for the given session (logout).</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);
}
