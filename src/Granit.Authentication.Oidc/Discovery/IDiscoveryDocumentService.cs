namespace Granit.Authentication.Oidc.Discovery;

/// <summary>
/// Fetches and caches OIDC discovery documents from identity providers.
/// Discovery documents are cached in memory and automatically refreshed.
/// </summary>
public interface IDiscoveryDocumentService
{
    /// <summary>
    /// Retrieves the OIDC discovery document for the given authority, using a cached copy if available.
    /// </summary>
    /// <param name="authority">The base URL of the identity provider (e.g., <c>"https://idp.example.com"</c>).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed <see cref="OidcDiscoveryDocument"/>.</returns>
    /// <exception cref="OidcDiscoveryException">
    /// Thrown when the discovery document cannot be fetched or parsed.
    /// </exception>
    Task<OidcDiscoveryDocument> GetAsync(string authority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cached discovery document for the given authority, forcing a refresh on the next call.
    /// </summary>
    /// <param name="authority">The base URL of the identity provider.</param>
    void InvalidateCache(string authority);
}
