using Granit.Documents.Authorization;
using Granit.Documents.Diagnostics;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Documents.EntityFrameworkCore.Authorization;

/// <summary>
/// FusionCache-backed decorator over <see cref="EffectivePermissionResolver"/> implementing
/// the F6.3 cache layer described in ADR-052 §Cache layer.
/// </summary>
/// <remarks>
/// <para>
/// On cache miss, delegates to <see cref="EffectivePermissionResolver.ResolveDocumentAsync"/>
/// to compute the effective level + ancestor folder ids, then writes the entry tagged with
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
    EffectivePermissionResolver inner,
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

        DocumentResolutionResult resolved = await inner
            .ResolveDocumentAsync(documentId, principal, cancellationToken)
            .ConfigureAwait(false);

        await cache.SetAsync(
            key,
            resolved.Permission,
            options: entryOptions,
            tags: AclCacheKeys.BuildEntryTags(documentId, resolved.AncestorFolderIds),
            token: cancellationToken).ConfigureAwait(false);

        return resolved.Permission;
    }
}
