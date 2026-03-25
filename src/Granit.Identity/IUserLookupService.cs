using Granit.QueryEngine;

namespace Granit.Identity;

/// <summary>
/// Provides fast, cached resolution of user identifiers to display information (name, email).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="NullUserLookupService"/> is registered by default (returns null / empty lists / no-ops).
/// Install <c>Granit.Identity.Federated.EntityFrameworkCore</c> to enable EF Core–backed caching with
/// on-demand, login-time, and webhook sync strategies.
/// </para>
/// <para>
/// This abstraction is provider-agnostic: it works with any <see cref="IIdentityProvider"/>
/// implementation (Keycloak, Entra ID, LDAP, etc.).
/// </para>
/// </remarks>
public interface IUserLookupService
{
    // -- Read (cache-aside) --

    /// <summary>
    /// Resolves a single user by their external identity provider ID.
    /// Returns cached data if fresh, otherwise fetches from the identity provider.
    /// </summary>
    /// <param name="userId">The user ID in the identity provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or <c>null</c> if not found in cache or provider.</returns>
    Task<IIdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves multiple users by their external identity provider IDs.
    /// </summary>
    /// <param name="userIds">The user IDs to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved users (may contain fewer items than requested if some users are unknown).</returns>
    Task<IReadOnlyList<IIdentityUser>> FindByIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches cached users by a free-text term (username, email, name).
    /// This is a local-only query — it does not call the identity provider.
    /// </summary>
    /// <param name="searchTerm">Free-text search term.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated result with matching users and total count.</returns>
    Task<PagedResult<IIdentityUser>> SearchAsync(
        string searchTerm,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);

    // -- Sync --

    /// <summary>
    /// Forces a refresh of a single user from the identity provider, ignoring cache staleness.
    /// </summary>
    /// <param name="userId">The user ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed user, or <c>null</c> if the provider cannot resolve the user.</returns>
    Task<IIdentityUser?> RefreshByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a full sync of all users from the identity provider into the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of users synchronized.</returns>
    Task<int> RefreshAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes only stale cache entries (those older than the staleness threshold).
    /// More efficient than <see cref="RefreshAllAsync"/> for large user bases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of users refreshed.</returns>
    Task<int> RefreshStaleAsync(CancellationToken cancellationToken = default);

    // -- RGPD --

    /// <summary>
    /// Permanently deletes the cached entry for a user (RGPD Art. 17 — right to erasure).
    /// </summary>
    /// <param name="userId">The user ID to erase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces personally identifiable information in the cached entry with anonymized data
    /// (RGPD Art. 18 — right to restriction of processing).
    /// </summary>
    /// <param name="userId">The user ID to pseudonymize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PseudonymizeByIdAsync(string userId, CancellationToken cancellationToken = default);
}
