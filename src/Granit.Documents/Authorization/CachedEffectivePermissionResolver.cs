using Granit.Documents.Diagnostics;
using Granit.Documents.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Documents.Authorization;

/// <summary>
/// FusionCache-backed decorator over an inner <see cref="IEffectivePermissionResolver"/>
/// implementing the F6.3 cache layer described in ADR-052 §Cache layer.
/// </summary>
/// <remarks>
/// <para>
/// On cache miss, delegates to the inner resolver's
/// <see cref="IEffectivePermissionResolver.ResolveDocumentWithAncestorsAsync"/> /
/// <see cref="IEffectivePermissionResolver.ResolveFoldersWithAncestorsAsync"/> to compute the
/// effective level + ancestor folder ids, then writes the entry tagged with
/// <c>acl:doc:{documentId}</c>, one <c>acl:folder:{folderId}</c> per ancestor, and the
/// tenant-wide <see cref="AclCacheKeys.AllTag"/>. Subsequent share grant / revoke events
/// invalidate by tag through <see cref="AclCacheInvalidationHandler"/>.
/// </para>
/// <para>
/// TTL comes from <see cref="GranitDocumentsOptions.AclCacheTtl"/> (default 5 minutes).
/// Cache hits and misses are recorded on <c>granit.documents.acl.cache.hits</c> /
/// <c>.misses</c>; the hit ratio is derived downstream by the observability pipeline.
/// </para>
/// </remarks>
internal sealed class CachedEffectivePermissionResolver(
    IEffectivePermissionResolver inner,
    IFusionCache cache,
    ICurrentTenant currentTenant,
    DocumentsMetrics metrics,
    IOptions<GranitDocumentsOptions> options) : IEffectivePermissionResolver
{
    /// <inheritdoc />
    public async Task<EffectivePermissionLevel> GetDocumentPermissionAsync(
        Guid documentId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Short-circuit: a principal with no grantee identities can never match a grant.
        // Mirrors the inner resolver's behaviour and avoids polluting the cache with No-result
        // entries for an empty key.
        if (principal.AllGranteeIds.Count == 0)
        {
            return EffectivePermissionLevel.None;
        }

        GranitDocumentsOptions opts = options.Value;
        string tenantTag = currentTenant.IsAvailable
            ? currentTenant.Id!.Value.ToString("N")
            : "global";
        string key = AclCacheKeys.Document(documentId, principal);

        FusionCacheEntryOptions entryOptions = cache.CreateEntryOptions(
            o => o.SetDuration(opts.AclCacheTtl),
            duration: opts.AclCacheTtl);

        // Try-get first so the entry's tag set can include the per-ancestor folder ids
        // computed during a real resolution. FusionCache's GetOrSetAsync evaluates the
        // tags parameter up front (before the factory runs) — splitting the call lets the
        // tag set match the resolution context exactly.
        MaybeValue<EffectivePermissionLevel> hit = await cache
            .TryGetAsync<EffectivePermissionLevel>(key, options: entryOptions, token: cancellationToken)
            .ConfigureAwait(false);
        if (hit.HasValue)
        {
            metrics.RecordAclCacheHit(tenantTag);
            return hit.Value;
        }

        metrics.RecordAclCacheMiss(tenantTag);

        DocumentPermissionResolution resolved = await inner
            .ResolveDocumentWithAncestorsAsync(documentId, principal, cancellationToken)
            .ConfigureAwait(false);

        await cache.SetAsync(
            key,
            resolved.Permission,
            options: entryOptions,
            tags: AclCacheKeys.BuildEntryTags(documentId, resolved.AncestorFolderIds),
            token: cancellationToken).ConfigureAwait(false);

        return resolved.Permission;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetDocumentPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<Guid, EffectivePermissionLevel> output = new(documentIds.Count);
        if (documentIds.Count == 0)
        {
            return output;
        }
        if (principal.AllGranteeIds.Count == 0)
        {
            foreach (Guid id in documentIds)
            {
                output[id] = EffectivePermissionLevel.None;
            }
            return output;
        }

        GranitDocumentsOptions opts = options.Value;
        string tenantTag = currentTenant.IsAvailable
            ? currentTenant.Id!.Value.ToString("N")
            : "global";
        FusionCacheEntryOptions entryOptions = cache.CreateEntryOptions(
            o => o.SetDuration(opts.AclCacheTtl),
            duration: opts.AclCacheTtl);

        // Phase 1 — try-get every id from cache; track which ones missed so the inner
        // batch resolution only pays for the cold rows.
        List<Guid> misses = [];
        Dictionary<Guid, string> keyByDocId = new(documentIds.Count);
        foreach (Guid documentId in documentIds)
        {
            string key = AclCacheKeys.Document(documentId, principal);
            keyByDocId[documentId] = key;
            MaybeValue<EffectivePermissionLevel> hit = await cache
                .TryGetAsync<EffectivePermissionLevel>(key, options: entryOptions, token: cancellationToken)
                .ConfigureAwait(false);
            if (hit.HasValue)
            {
                metrics.RecordAclCacheHit(tenantTag);
                output[documentId] = hit.Value;
            }
            else
            {
                metrics.RecordAclCacheMiss(tenantTag);
                misses.Add(documentId);
            }
        }

        if (misses.Count == 0)
        {
            return output;
        }

        // Phase 2 — single batched DB resolution for the misses, then warm each cache
        // entry with its proper tag set (per-doc ancestor folder ids).
        IReadOnlyDictionary<Guid, DocumentPermissionResolution> resolved = await inner
            .ResolveDocumentsWithAncestorsAsync(misses, principal, cancellationToken)
            .ConfigureAwait(false);

        foreach (Guid documentId in misses)
        {
            DocumentPermissionResolution r = resolved.TryGetValue(documentId, out DocumentPermissionResolution v)
                ? v
                : new DocumentPermissionResolution(EffectivePermissionLevel.None, []);
            output[documentId] = r.Permission;
            await cache.SetAsync(
                keyByDocId[documentId],
                r.Permission,
                options: entryOptions,
                tags: AclCacheKeys.BuildEntryTags(documentId, r.AncestorFolderIds),
                token: cancellationToken).ConfigureAwait(false);
        }

        return output;
    }

    /// <inheritdoc />
    public async Task<EffectivePermissionLevel> GetFolderPermissionAsync(
        Guid folderId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.AllGranteeIds.Count == 0)
        {
            return EffectivePermissionLevel.None;
        }

        GranitDocumentsOptions opts = options.Value;
        string tenantTag = currentTenant.IsAvailable
            ? currentTenant.Id!.Value.ToString("N")
            : "global";
        string key = AclCacheKeys.Folder(folderId, principal);

        FusionCacheEntryOptions entryOptions = cache.CreateEntryOptions(
            o => o.SetDuration(opts.AclCacheTtl),
            duration: opts.AclCacheTtl);

        MaybeValue<EffectivePermissionLevel> hit = await cache
            .TryGetAsync<EffectivePermissionLevel>(key, options: entryOptions, token: cancellationToken)
            .ConfigureAwait(false);
        if (hit.HasValue)
        {
            metrics.RecordAclCacheHit(tenantTag);
            return hit.Value;
        }

        metrics.RecordAclCacheMiss(tenantTag);

        IReadOnlyDictionary<Guid, FolderPermissionResolution> resolved = await inner
            .ResolveFoldersWithAncestorsAsync([folderId], principal, cancellationToken)
            .ConfigureAwait(false);
        FolderPermissionResolution r = resolved.TryGetValue(folderId, out FolderPermissionResolution v)
            ? v
            : new FolderPermissionResolution(EffectivePermissionLevel.None, []);

        await cache.SetAsync(
            key,
            r.Permission,
            options: entryOptions,
            tags: AclCacheKeys.BuildFolderEntryTags(r.AncestorFolderIds),
            token: cancellationToken).ConfigureAwait(false);

        return r.Permission;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetFolderPermissionsAsync(
        IReadOnlyCollection<Guid> folderIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderIds);
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<Guid, EffectivePermissionLevel> output = new(folderIds.Count);
        if (folderIds.Count == 0)
        {
            return output;
        }
        if (principal.AllGranteeIds.Count == 0)
        {
            foreach (Guid id in folderIds)
            {
                output[id] = EffectivePermissionLevel.None;
            }
            return output;
        }

        GranitDocumentsOptions opts = options.Value;
        string tenantTag = currentTenant.IsAvailable
            ? currentTenant.Id!.Value.ToString("N")
            : "global";
        FusionCacheEntryOptions entryOptions = cache.CreateEntryOptions(
            o => o.SetDuration(opts.AclCacheTtl),
            duration: opts.AclCacheTtl);

        List<Guid> misses = [];
        Dictionary<Guid, string> keyByFolderId = new(folderIds.Count);
        foreach (Guid folderId in folderIds)
        {
            string key = AclCacheKeys.Folder(folderId, principal);
            keyByFolderId[folderId] = key;
            MaybeValue<EffectivePermissionLevel> hit = await cache
                .TryGetAsync<EffectivePermissionLevel>(key, options: entryOptions, token: cancellationToken)
                .ConfigureAwait(false);
            if (hit.HasValue)
            {
                metrics.RecordAclCacheHit(tenantTag);
                output[folderId] = hit.Value;
            }
            else
            {
                metrics.RecordAclCacheMiss(tenantTag);
                misses.Add(folderId);
            }
        }

        if (misses.Count == 0)
        {
            return output;
        }

        IReadOnlyDictionary<Guid, FolderPermissionResolution> resolved = await inner
            .ResolveFoldersWithAncestorsAsync(misses, principal, cancellationToken)
            .ConfigureAwait(false);

        foreach (Guid folderId in misses)
        {
            FolderPermissionResolution r = resolved.TryGetValue(folderId, out FolderPermissionResolution v)
                ? v
                : new FolderPermissionResolution(EffectivePermissionLevel.None, []);
            output[folderId] = r.Permission;
            await cache.SetAsync(
                keyByFolderId[folderId],
                r.Permission,
                options: entryOptions,
                tags: AclCacheKeys.BuildFolderEntryTags(r.AncestorFolderIds),
                token: cancellationToken).ConfigureAwait(false);
        }

        return output;
    }

    /// <inheritdoc />
    public Task<DocumentPermissionResolution> ResolveDocumentWithAncestorsAsync(
        Guid documentId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default) =>
        // The cache layer keys on permission level only — ancestor folder ids are part of the
        // tag set, not the cached value. Delegate straight to the inner resolver so callers
        // that need ancestor data (e.g. the cache miss path itself, or diagnostics) hit the
        // database. The hot path uses the cached Get(Document|Folder)Permission(s)Async API.
        inner.ResolveDocumentWithAncestorsAsync(documentId, principal, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, DocumentPermissionResolution>> ResolveDocumentsWithAncestorsAsync(
        IReadOnlyCollection<Guid> documentIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default) =>
        inner.ResolveDocumentsWithAncestorsAsync(documentIds, principal, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, FolderPermissionResolution>> ResolveFoldersWithAncestorsAsync(
        IReadOnlyCollection<Guid> folderIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default) =>
        inner.ResolveFoldersWithAncestorsAsync(folderIds, principal, cancellationToken);
}
