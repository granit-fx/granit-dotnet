using Granit.Identity.Federated.Domain;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Internal data access layer for identity user cache entries.
/// Provides CRUD, search, GDPR, and diagnostic operations on <see cref="FederatedIdentity"/>.
/// </summary>
internal interface IUserCacheStore
{
    // -- Read --

    /// <summary>Finds a cached user by external ID within a specific tenant scope.</summary>
    Task<FederatedIdentity?> FindByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Finds the first cached entry for an external user ID, regardless of tenant (host context).</summary>
    Task<FederatedIdentity?> FindFirstByExternalIdAsync(
        string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup of cached users by external IDs within a tenant scope.</summary>
    Task<IReadOnlyList<FederatedIdentity>> FindByExternalIdsAsync(
        IReadOnlyCollection<string> externalUserIds, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Searches cached users by free-text term (username, email, first name, last name) with pagination.</summary>
    Task<(IReadOnlyList<FederatedIdentity> Items, int TotalCount)> SearchAsync(
        string term, Guid? tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    // -- Diagnostics --

    /// <summary>Returns the total number of cached entries for a tenant.</summary>
    Task<int> GetCountAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns the number of stale entries (LastSyncedAt older than threshold).</summary>
    Task<int> GetStaleCountAsync(Guid? tenantId, DateTimeOffset threshold, CancellationToken cancellationToken = default);

    /// <summary>Returns external user IDs of stale entries (LastSyncedAt older than threshold), batched.</summary>
    Task<IReadOnlyList<string>> FindStaleExternalIdsAsync(
        Guid? tenantId, DateTimeOffset threshold, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Returns the oldest and newest sync timestamps for a tenant.</summary>
    Task<(DateTimeOffset? Oldest, DateTimeOffset? Newest)> GetSyncRangeAsync(
        Guid? tenantId, CancellationToken cancellationToken = default);

    // -- Write --

    /// <summary>
    /// Inserts or updates a single cache entry (matched by TenantId + ExternalUserId) and returns
    /// the persisted row's <c>Id</c>. When a concurrent insert wins the race for the same key, the
    /// returned Id is the winner's — different from <paramref name="entry"/>.<c>Id</c> — so the
    /// caller can compensate for any resources it pre-created against its own (losing) Id.
    /// </summary>
    Task<Guid> UpsertAsync(FederatedIdentity entry, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates multiple cache entries in batch.</summary>
    Task UpsertManyAsync(IReadOnlyList<FederatedIdentity> entries, CancellationToken cancellationToken = default);

    // -- GDPR --

    /// <summary>
    /// Permanently deletes the cache entry for a user (GDPR Art. 17) and returns the number of
    /// rows removed. A <c>null</c> <paramref name="tenantId"/> erases the user's mirror across
    /// ALL tenant partitions (the multi-tenant query filter is explicitly bypassed); a non-null
    /// scope deletes within that tenant only and requires the caller to have established a
    /// matching ambient tenant (<c>ICurrentTenant.Change</c>) so the tenant filter exposes the row.
    /// </summary>
    Task<int> DeleteByExternalIdAsync(string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Purges all cache entries for a tenant.</summary>
    Task DeleteAllByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Replaces PII with anonymized data (GDPR Art. 18).</summary>
    Task PseudonymizeAsync(string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);
}
