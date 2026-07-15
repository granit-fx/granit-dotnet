using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Default <see cref="IUserCacheStats"/> implementation: tenant-scoped counts and sync range
/// derived by delegating to <see cref="IUserCacheStore"/> — no EF Core types of its own.
/// </summary>
internal sealed class UserCacheStats(
    IUserCacheStore store,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IOptions<UserCacheOptions> options) : IUserCacheStats
{
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        return store.GetCountAsync(tenantId, cancellationToken);
    }

    public Task<int> GetStaleCountAsync(CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        DateTimeOffset threshold = timeProvider.GetUtcNow() - options.Value.StalenessThreshold;
        return store.GetStaleCountAsync(tenantId, threshold, cancellationToken);
    }

    public Task<(DateTimeOffset? Oldest, DateTimeOffset? Newest)> GetSyncRangeAsync(
        CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        return store.GetSyncRangeAsync(tenantId, cancellationToken);
    }
}
