using Granit.Guids;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Options;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Cache-aside implementation of <see cref="IUserLookupService"/>.
/// Replaces <c>NullUserLookupService</c> when <c>Granit.Identity.Federated.EntityFrameworkCore</c> is registered.
/// </summary>
internal sealed partial class CachedUserLookupService(
    IUserCacheStore store,
    IIdentityProvider identityProvider,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider,
    IGuidGenerator guidGenerator,
    IOptions<UserCacheOptions> options,
    ILogger<CachedUserLookupService> logger) : IUserLookupService
{
    private readonly UserCacheOptions _options = options.Value;

    // -- Read (cache-aside) --

    public async Task<IIdentityUser?> FindByIdAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        // Tenant-scoped or host-context lookup
        FederatedIdentity? entry = currentTenant.IsAvailable
            ? await store.FindByExternalIdAsync(userId, currentTenant.Id, cancellationToken).ConfigureAwait(false)
            : await store.FindFirstByExternalIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (entry is not null && IsFresh(entry))
        {
            return entry;
        }

        // Cache miss or stale — fetch from identity provider
        try
        {
            IIdentityUser? providerUser = await identityProvider.GetUserAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            if (providerUser is not null)
            {
                FederatedIdentity cacheEntry = ToCacheEntry(providerUser);
                await store.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
                return providerUser;
            }

            // Provider returned null — user doesn't exist in provider
            return entry;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Graceful degradation: provider down, return stale data if available
            LogProviderError(ex, userId);
            return entry;
        }
    }

    public async Task<IReadOnlyList<IIdentityUser>> FindByIdsAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        IReadOnlyList<FederatedIdentity> cached = await store.FindByExternalIdsAsync(userIds, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var result = new List<IIdentityUser>(userIds.Count);
        var cachedDict = cached.ToDictionary(e => e.ExternalUserId);
        List<string> toFetch = [];

        foreach (string id in userIds)
        {
            if (cachedDict.TryGetValue(id, out FederatedIdentity? entry) && IsFresh(entry))
            {
                result.Add(entry);
            }
            else
            {
                toFetch.Add(id);
            }
        }

        if (toFetch.Count > 0)
        {
            await FetchMissingUsersAsync(toFetch, cachedDict, result, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task FetchMissingUsersAsync(
        List<string> toFetch,
        Dictionary<string, FederatedIdentity> cachedDict,
        List<IIdentityUser> result,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (string id in toFetch)
            {
                IIdentityUser? providerUser = await identityProvider.GetUserAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                if (providerUser is not null)
                {
                    FederatedIdentity cacheEntry = ToCacheEntry(providerUser);
                    await store.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
                    result.Add(providerUser);
                }
                else if (cachedDict.TryGetValue(id, out FederatedIdentity? staleEntry))
                {
                    result.Add(staleEntry);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProviderBatchError(ex, toFetch.Count);
            AddStaleEntries(toFetch, cachedDict, result);
        }
    }

    private static void AddStaleEntries(
        List<string> ids,
        Dictionary<string, FederatedIdentity> cachedDict,
        List<IIdentityUser> result)
    {
        foreach (string id in ids)
        {
            if (cachedDict.TryGetValue(id, out FederatedIdentity? staleEntry)
                && !result.Any(u => u.UserId == id))
            {
                result.Add(staleEntry);
            }
        }
    }

    public async Task<PagedResult<IIdentityUser>> SearchAsync(
        string searchTerm, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        (IReadOnlyList<FederatedIdentity> entries, int totalCount) = await store
            .SearchAsync(searchTerm, tenantId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = entries.Cast<IIdentityUser>().ToList();
        (int clampedPage, int clampedPageSize) = QueryEngineDefaults.ClampPagination(page, pageSize);
        int skip = (clampedPage - 1) * clampedPageSize;
        return new PagedResult<IIdentityUser>(items, totalCount, HasMore: skip + items.Count < totalCount);
    }

    // -- Sync --

    public async Task<IIdentityUser?> RefreshByIdAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        IIdentityUser? providerUser = await identityProvider.GetUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (providerUser is null)
        {
            return null;
        }

        FederatedIdentity cacheEntry = ToCacheEntry(providerUser);
        await store.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
        return providerUser;
    }

    public async Task<int> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        int synced = 0;
        int offset = 0;
        const int pageSize = 100;

        while (true)
        {
            IReadOnlyList<IIdentityUser> page = await identityProvider.GetUsersAsync(
                search: null, first: offset, max: pageSize, cancellationToken).ConfigureAwait(false);

            if (page.Count == 0)
            {
                break;
            }

            var entries = page.Select(ToCacheEntry).ToList();
            await store.UpsertManyAsync(entries, cancellationToken).ConfigureAwait(false);

            synced += page.Count;
            offset += page.Count;

            if (page.Count < pageSize)
            {
                break;
            }
        }

        LogRefreshAllCompleted(synced);
        return synced;
    }

    public async Task<int> RefreshStaleAsync(CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        DateTimeOffset threshold = timeProvider.GetUtcNow() - _options.StalenessThreshold;

        IReadOnlyList<string> staleIds = await store.FindStaleExternalIdsAsync(
            tenantId, threshold, _options.IncrementalSyncBatchSize, cancellationToken).ConfigureAwait(false);

        int refreshed = 0;

        foreach (string userId in staleIds)
        {
            IIdentityUser? providerUser = await identityProvider.GetUserAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            if (providerUser is not null)
            {
                FederatedIdentity cacheEntry = ToCacheEntry(providerUser);
                await store.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
                refreshed++;
            }
        }

        LogRefreshStaleCompleted(refreshed, staleIds.Count);
        return refreshed;
    }

    // -- GDPR --

    public async Task DeleteByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        await store.DeleteByExternalIdAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        LogGdprDelete(userId);
    }

    public async Task PseudonymizeByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        await store.PseudonymizeAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        LogGdprPseudonymize(userId);
    }

    // -- Helpers --

    private bool IsFresh(FederatedIdentity entry) =>
        timeProvider.GetUtcNow() - entry.LastSyncedAt < _options.StalenessThreshold;

    private FederatedIdentity ToCacheEntry(IIdentityUser user)
    {
        // Per ADR-051 B-step 3, pre-assign the Guid so FederatedIdentity.UserId
        // can be aligned with .Id at construction time. The store preserves
        // these identifiers on update — only an INSERT path adopts the
        // freshly-generated value, so the alignment invariant
        // (FederatedIdentity.Id == FederatedIdentity.UserId == User.Id)
        // is enforced from the very first row.
        Guid id = guidGenerator.Create();
        return new FederatedIdentity
        {
            Id = id,
            UserId = id,
            ExternalUserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Enabled = user.Enabled,
            LastSyncedAt = timeProvider.GetUtcNow(),
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
        };
    }

    // -- Source-generated log messages --

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity provider error while fetching user {UserId}, returning stale cache")]
    private partial void LogProviderError(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity provider error during batch fetch of {Count} users, returning stale cache")]
    private partial void LogProviderBatchError(Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] Full user cache refresh completed: {Count} users synchronized")]
    private partial void LogRefreshAllCompleted(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] Incremental user cache refresh completed: {Refreshed}/{Total} stale entries refreshed")]
    private partial void LogRefreshStaleCompleted(int refreshed, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] GDPR erasure: user cache entry deleted for user {UserId}")]
    private partial void LogGdprDelete(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[AUDIT] GDPR pseudonymization: user cache entry anonymized for user {UserId}")]
    private partial void LogGdprPseudonymize(string userId);
}
