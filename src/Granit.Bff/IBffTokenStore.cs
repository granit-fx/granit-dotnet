namespace Granit.Bff;

/// <summary>
/// Stores and retrieves BFF session tokens in a distributed cache.
/// Tokens are keyed by frontend name + session ID.
/// </summary>
public interface IBffTokenStore
{
    /// <summary>Stores a token set for the given frontend session.</summary>
    /// <param name="frontendName">The frontend name (e.g., "admin", "patient").</param>
    /// <param name="sessionId">The session identifier (from the session cookie).</param>
    /// <param name="tokens">The token set to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreAsync(string frontendName, string sessionId, BffTokenSet tokens, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the token set for the given frontend session, or <c>null</c> if expired or missing.</summary>
    /// <param name="frontendName">The frontend name.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BffTokenSet?> GetAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Removes the token set for the given frontend session (logout).</summary>
    /// <param name="frontendName">The frontend name.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default);
}
