using Granit.Identity.Federated.EntityFrameworkCore.Entities;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// Internal data access layer for identity user cache entries.
/// Provides CRUD, search, RGPD, and diagnostic operations on <see cref="UserCacheEntry"/>.
/// </summary>
internal interface IUserCacheStore
{
    // -- Read --

    /// <summary>Finds a cached user by external ID within a specific tenant scope.</summary>
    Task<UserCacheEntry?> FindByExternalIdAsync(
        string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Finds the first cached entry for an external user ID, regardless of tenant (host context).</summary>
    Task<UserCacheEntry?> FindFirstByExternalIdAsync(
        string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup of cached users by external IDs within a tenant scope.</summary>
    Task<IReadOnlyList<UserCacheEntry>> FindByExternalIdsAsync(
        IReadOnlyCollection<string> externalUserIds, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Searches cached users by free-text term (username, email, first name, last name) with pagination.</summary>
    Task<(IReadOnlyList<UserCacheEntry> Items, int TotalCount)> SearchAsync(
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

    /// <summary>Inserts or updates a single cache entry (matched by TenantId + ExternalUserId).</summary>
    Task UpsertAsync(UserCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates multiple cache entries in batch.</summary>
    Task UpsertManyAsync(IReadOnlyList<UserCacheEntry> entries, CancellationToken cancellationToken = default);

    // -- RGPD --

    /// <summary>Permanently deletes the cache entry for a user (RGPD Art. 17).</summary>
    Task DeleteByExternalIdAsync(string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Purges all cache entries for a tenant.</summary>
    Task DeleteAllByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Replaces PII with anonymized data (RGPD Art. 18).</summary>
    Task PseudonymizeAsync(string externalUserId, Guid? tenantId, CancellationToken cancellationToken = default);
}
