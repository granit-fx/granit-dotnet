namespace Granit.Identity;

/// <summary>
/// Provides diagnostic statistics about the identity user cache.
/// </summary>
/// <remarks>
/// A null-object implementation is registered by default (returns zeroes).
/// Install <c>Granit.Identity.Federated.EntityFrameworkCore</c> to enable real statistics.
/// </remarks>
public interface IUserCacheStats
{
    /// <summary>Returns the total number of cached entries for the current tenant.</summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the number of stale entries (past the configured threshold).</summary>
    Task<int> GetStaleCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the oldest and newest sync timestamps for the current tenant.</summary>
    Task<(DateTimeOffset? Oldest, DateTimeOffset? Newest)> GetSyncRangeAsync(
        CancellationToken cancellationToken = default);
}
