using Granit.Authentication.ApiKeys.Domain;
using Granit.QueryEngine;

namespace Granit.Authentication.ApiKeys;

/// <summary>
/// Administrative persistence operations for API keys (CRUD, revocation, rotation).
/// Extends <see cref="IApiKeyStore"/> with write operations exposed by management endpoints.
/// </summary>
public interface IApiKeyAdminStore
{
    /// <summary>
    /// Finds an API key by its unique identifier.
    /// </summary>
    Task<ApiKeyEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of API keys filtered by optional criteria.
    /// </summary>
    /// <param name="search">Optional text to match against the key name.</param>
    /// <param name="type">Optional filter by key type.</param>
    /// <param name="environment">Optional filter by target environment.</param>
    /// <param name="includeRevoked">Whether to include revoked keys. Default: <c>false</c>.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<ApiKeyEntry>> ListAsync(
        string? search = null,
        ApiKeyType? type = null,
        string? environment = null,
        bool includeRevoked = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new API key entry.
    /// </summary>
    Task CreateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an API key as revoked by setting <see cref="ApiKeyEntry.RevokedAt"/>.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the permissions and allowed CIDR ranges for an API key.
    /// </summary>
    Task<bool> UpdateScopesAsync(
        Guid id,
        List<string> permissions,
        List<string> allowedCidrs,
        CancellationToken cancellationToken = default);
}
