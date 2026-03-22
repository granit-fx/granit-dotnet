using Granit.Core.MultiTenancy;
using Granit.Identity.Federated.EntityFrameworkCore.Options;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserCacheStats"/>.
/// Delegates to <see cref="IUserCacheStore"/> for all queries.
/// </summary>
internal sealed class EfCoreUserCacheStats(
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
