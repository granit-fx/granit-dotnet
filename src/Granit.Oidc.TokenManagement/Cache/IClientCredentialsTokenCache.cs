namespace Granit.Oidc.TokenManagement.Cache;

/// <summary>
/// Caches access tokens obtained via the client credentials grant.
/// Implementations must be safe for concurrent access across multiple HTTP clients.
/// </summary>
public interface IClientCredentialsTokenCache
{
    /// <summary>
    /// Retrieves a cached access token for the specified client name.
    /// </summary>
    /// <param name="clientName">The named client configuration identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cached access token, or <see langword="null"/> if not found or expired.</returns>
    Task<string?> GetTokenAsync(string clientName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an access token in the cache with the specified expiry.
    /// </summary>
    /// <param name="clientName">The named client configuration identifier.</param>
    /// <param name="accessToken">The access token to cache.</param>
    /// <param name="expiry">The duration after which the cached token expires.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetTokenAsync(string clientName, string accessToken, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached access token for the specified client name.
    /// </summary>
    /// <param name="clientName">The named client configuration identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveTokenAsync(string clientName, CancellationToken cancellationToken = default);
}
