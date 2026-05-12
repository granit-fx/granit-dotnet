using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed <see cref="IEffectivePermissionResolver"/> implementing the ADR-052
/// path-prefix permission resolution model.
/// </summary>
/// <remarks>
/// <para>
/// The resolver issues two queries against the isolated <c>DocumentsDbContext</c>:
/// </para>
/// <list type="number">
///   <item>
///     A primary-key lookup that joins the requested document with its folder to retrieve
///     <c>TenantId</c>, <c>FolderId</c>, and the materialised <c>Folder.Path</c>. This is a
///     single-row PK fetch and stays cheap regardless of cache layer.
///   </item>
///   <item>
///     The share-resolution query — direct document grants OR folder grants whose folder's
///     <c>Path</c> sits in the document's ancestor chain — filtered to the principal's
///     grantee ids and to non-expired grants. Returns the matching <c>Permission</c> values;
///     the caller takes the highest.
///   </item>
/// </list>
/// <para>
/// Splitting the work avoids EF Core translating the inline ancestor-paths
/// expansion into SQL on every dialect — the path tokens are computed in C# from the
/// materialised <c>Folder.Path</c> and shipped as a parameter list. The resulting share
/// query lights up the filtered indexes (<c>ix_documents_shares_grantee_folder</c> /
/// <c>ix_documents_shares_grantee_document</c>) introduced in F6.1.
/// </para>
/// </remarks>
internal sealed class EffectivePermissionResolver(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IClock clock) : IEffectivePermissionResolver
{
    /// <inheritdoc />
    public async Task<EffectivePermissionLevel> GetDocumentPermissionAsync(
        Guid documentId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        DocumentPermissionResolution result = await ResolveDocumentWithAncestorsAsync(
            documentId, principal, cancellationToken).ConfigureAwait(false);
        return result.Permission;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetDocumentPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, DocumentPermissionResolution> resolved = await ResolveDocumentsWithAncestorsAsync(
            documentIds, principal, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, EffectivePermissionLevel> result = new(resolved.Count);
        foreach ((Guid id, DocumentPermissionResolution r) in resolved)
        {
            result[id] = r.Permission;
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<EffectivePermissionLevel> GetFolderPermissionAsync(
        Guid folderId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, FolderPermissionResolution> resolved = await ResolveFoldersWithAncestorsAsync(
            [folderId], principal, cancellationToken).ConfigureAwait(false);
        return resolved.TryGetValue(folderId, out FolderPermissionResolution r)
            ? r.Permission
            : EffectivePermissionLevel.None;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetFolderPermissionsAsync(
        IReadOnlyCollection<Guid> folderIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, FolderPermissionResolution> resolved = await ResolveFoldersWithAncestorsAsync(
            folderIds, principal, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, EffectivePermissionLevel> result = new(resolved.Count);
        foreach ((Guid id, FolderPermissionResolution r) in resolved)
        {
            result[id] = r.Permission;
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<DocumentPermissionResolution> ResolveDocumentWithAncestorsAsync(
        Guid documentId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Materialise as a concrete List<Guid> — EF Core's Contains translator is pickier
        // about IReadOnlyList<T> than List<T> in some predicate shapes and the principal's
        // grantee snapshot is small (user + role count + group count).
        List<Guid> granteeIds = [.. principal.AllGranteeIds];
        if (granteeIds.Count == 0)
        {
            return new DocumentPermissionResolution(EffectivePermissionLevel.None, []);
        }

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 1 — locate the document's folder + materialised path. Tenant scoping is
        // applied implicitly by the named query filter on Document/Folder.
        var docInfo = await context.Documents
            .Where(d => d.Id == documentId)
            .Join(context.Folders,
                d => d.FolderId,
                f => f.Id,
                (d, f) => new { d.TenantId, FolderPath = f.Path })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (docInfo is null)
        {
            return new DocumentPermissionResolution(EffectivePermissionLevel.None, []);
        }

        // Build the ancestor-path set in C# (cheap string ops on a materialised path) and
        // ship it as a parameter list. Includes the document's own folder path so a direct
        // share on that folder participates in the same predicate as ancestor shares.
        List<string> ancestorPaths = ExpandSelfAndAncestorPaths(docInfo.FolderPath);

        // Step 2 — resolve the ancestor + self folder ids by their materialised paths. This
        // separate query is cheap (covered by ix_documents_folders_tenant_path) and lets
        // step 3 reduce to a simple Contains predicate that every EF Core provider can
        // translate — the alternative (a nested Folders.Any inside the share Where clause)
        // doesn't translate on SQLite.
        List<Guid> ancestorFolderIds = ancestorPaths.Count == 0
            ? []
            : await context.Folders
                .Where(f => f.TenantId == docInfo.TenantId
                    && ancestorPaths.Contains(f.Path))
                .Select(f => f.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        // Materialise as List<Guid?> for the share-IN clause — Folder.FolderId is nullable
        // and SQLite refused a Contains(.Value) translation; nullable list works everywhere.
        List<Guid?> ancestorFolderIdsForShareLookup = [.. ancestorFolderIds.Select(id => (Guid?)id)];

        DateTimeOffset now = clock.Now;

        // Step 3 — share-resolution. Splitting the direct-document and folder branches into
        // two queries makes each one index-friendly (each lights up a single filtered index
        // — ix_documents_shares_grantee_document or ix_documents_shares_grantee_folder)
        // and stays translatable across every EF Core provider including SQLite, which
        // chokes on the nested OR shape combining a Contains over a nullable column.
        // The IsDefault flag is intentionally NOT filtered: per ADR-052 / F6.4, all folder
        // shares inherit in phase 1 — IsDefault is reserved for phase 2 non-inherited shares.
        // The ExpiresAt filter is applied in C# rather than SQL because the
        // <c>ExpiresAt is null OR ExpiresAt &gt; now</c> shape combined with the rest of
        // the predicate trips up the SQLite translator. The filtered indexes already
        // narrow the row set to a handful of grants so the in-memory pass is essentially
        // free.
        var directMatches = await context.DocumentShares
            .Where(s => s.TenantId == docInfo.TenantId
                && granteeIds.Contains(s.GranteeId)
                && s.TargetType == ShareTargetType.Document
                && s.DocumentId == documentId)
            .Select(s => new { s.Permission, s.ExpiresAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var folderMatches = ancestorFolderIdsForShareLookup.Count == 0
            ? []
            : await context.DocumentShares
                .Where(s => s.TenantId == docInfo.TenantId
                    && granteeIds.Contains(s.GranteeId)
                    && s.TargetType == ShareTargetType.Folder
                    && ancestorFolderIdsForShareLookup.Contains(s.FolderId))
                .Select(s => new { s.Permission, s.ExpiresAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        List<SharePermissionLevel> matches = [
            .. directMatches.Where(m => m.ExpiresAt is null || m.ExpiresAt > now).Select(m => m.Permission),
            .. folderMatches.Where(m => m.ExpiresAt is null || m.ExpiresAt > now).Select(m => m.Permission),
        ];

        EffectivePermissionLevel level = matches.Count == 0
            ? EffectivePermissionLevel.None
            : matches.Max() switch
            {
                SharePermissionLevel.Manage => EffectivePermissionLevel.Manage,
                SharePermissionLevel.Edit => EffectivePermissionLevel.Edit,
                SharePermissionLevel.Read => EffectivePermissionLevel.Read,
                _ => EffectivePermissionLevel.None,
            };

        return new DocumentPermissionResolution(level, ancestorFolderIds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, DocumentPermissionResolution>> ResolveDocumentsWithAncestorsAsync(
        IReadOnlyCollection<Guid> documentIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<Guid, DocumentPermissionResolution> output = new(documentIds.Count);
        if (documentIds.Count == 0)
        {
            return output;
        }

        DocumentPermissionResolution empty = new(EffectivePermissionLevel.None, []);
        foreach (Guid id in documentIds)
        {
            output[id] = empty;
        }

        List<Guid> granteeIds = [.. principal.AllGranteeIds];
        if (granteeIds.Count == 0)
        {
            return output;
        }

        List<Guid> idList = [.. documentIds];

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 1 — fetch (DocumentId, TenantId, FolderPath) for every requested document.
        var docInfos = await context.Documents
            .Where(d => idList.Contains(d.Id))
            .Join(context.Folders,
                d => d.FolderId,
                f => f.Id,
                (d, f) => new { d.Id, d.TenantId, FolderPath = f.Path })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (docInfos.Count == 0)
        {
            return output;
        }

        // Step 2 — union of self+ancestor paths across the page; deduplicate so a single
        // folder lookup covers documents that share ancestry. Tenant scoping is applied
        // via the document's TenantId (the page typically belongs to one tenant — the
        // query filter guarantees it — but the predicate is per-tenant either way).
        Guid tenantId = docInfos[0].TenantId!.Value;
        Dictionary<Guid, List<string>> docPaths = new(docInfos.Count);
        HashSet<string> uniquePaths = [];
        foreach (var info in docInfos)
        {
            List<string> paths = ExpandSelfAndAncestorPaths(info.FolderPath);
            docPaths[info.Id] = paths;
            foreach (string p in paths)
            {
                uniquePaths.Add(p);
            }
        }

        // Step 3 — resolve all unique ancestor paths to folder ids in one query.
        Dictionary<string, Guid> pathToFolderId = uniquePaths.Count == 0
            ? []
            : await context.Folders
                .Where(f => f.TenantId == tenantId && uniquePaths.Contains(f.Path))
                .Select(f => new { f.Id, f.Path })
                .ToDictionaryAsync(x => x.Path, x => x.Id, cancellationToken)
                .ConfigureAwait(false);

        List<Guid?> allAncestorFolderIds = [.. pathToFolderId.Values.Select(id => (Guid?)id)];

        // Step 4 — direct document shares matching any of the requested document ids.
        var directShares = await context.DocumentShares
            .Where(s => s.TenantId == tenantId
                && granteeIds.Contains(s.GranteeId)
                && s.TargetType == ShareTargetType.Document
                && s.DocumentId != null
                && idList.Contains(s.DocumentId!.Value))
            .Select(s => new { s.DocumentId, s.Permission, s.ExpiresAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 5 — folder shares matching any folder in the union of ancestor sets.
        var folderShares = allAncestorFolderIds.Count == 0
            ? []
            : await context.DocumentShares
                .Where(s => s.TenantId == tenantId
                    && granteeIds.Contains(s.GranteeId)
                    && s.TargetType == ShareTargetType.Folder
                    && allAncestorFolderIds.Contains(s.FolderId))
                .Select(s => new { s.FolderId, s.Permission, s.ExpiresAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;

        // Index folder shares by FolderId for O(1) lookup per document.
        Dictionary<Guid, List<SharePermissionLevel>> folderShareIndex = [];
        foreach (var fs in folderShares)
        {
            if (fs.ExpiresAt is { } exp && exp <= now)
            {
                continue;
            }
            if (fs.FolderId is not { } fid)
            {
                continue;
            }
            if (!folderShareIndex.TryGetValue(fid, out List<SharePermissionLevel>? bucket))
            {
                bucket = [];
                folderShareIndex[fid] = bucket;
            }
            bucket.Add(fs.Permission);
        }

        Dictionary<Guid, List<SharePermissionLevel>> directShareIndex = [];
        foreach (var ds in directShares)
        {
            if (ds.ExpiresAt is { } exp && exp <= now)
            {
                continue;
            }
            if (ds.DocumentId is not { } did)
            {
                continue;
            }
            if (!directShareIndex.TryGetValue(did, out List<SharePermissionLevel>? bucket))
            {
                bucket = [];
                directShareIndex[did] = bucket;
            }
            bucket.Add(ds.Permission);
        }

        // Step 6 — compose the per-document effective permission.
        foreach (var info in docInfos)
        {
            List<string> paths = docPaths[info.Id];
            List<Guid> ancestorFolderIds = [];
            List<SharePermissionLevel> matches = [];
            foreach (string path in paths)
            {
                if (!pathToFolderId.TryGetValue(path, out Guid fid))
                {
                    continue;
                }
                ancestorFolderIds.Add(fid);
                if (folderShareIndex.TryGetValue(fid, out List<SharePermissionLevel>? bucket))
                {
                    matches.AddRange(bucket);
                }
            }
            if (directShareIndex.TryGetValue(info.Id, out List<SharePermissionLevel>? direct))
            {
                matches.AddRange(direct);
            }

            EffectivePermissionLevel level = matches.Count == 0
                ? EffectivePermissionLevel.None
                : matches.Max() switch
                {
                    SharePermissionLevel.Manage => EffectivePermissionLevel.Manage,
                    SharePermissionLevel.Edit => EffectivePermissionLevel.Edit,
                    SharePermissionLevel.Read => EffectivePermissionLevel.Read,
                    _ => EffectivePermissionLevel.None,
                };
            output[info.Id] = new DocumentPermissionResolution(level, ancestorFolderIds);
        }

        return output;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, FolderPermissionResolution>> ResolveFoldersWithAncestorsAsync(
        IReadOnlyCollection<Guid> folderIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderIds);
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<Guid, FolderPermissionResolution> output = new(folderIds.Count);
        if (folderIds.Count == 0)
        {
            return output;
        }

        FolderPermissionResolution empty = new(EffectivePermissionLevel.None, []);
        foreach (Guid id in folderIds)
        {
            output[id] = empty;
        }

        List<Guid> granteeIds = [.. principal.AllGranteeIds];
        if (granteeIds.Count == 0)
        {
            return output;
        }

        List<Guid> idList = [.. folderIds];

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 1 — fetch (Id, TenantId, Path) for every requested folder.
        var folderInfos = await context.Folders
            .Where(f => idList.Contains(f.Id))
            .Select(f => new { f.Id, f.TenantId, f.Path })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (folderInfos.Count == 0)
        {
            return output;
        }

        Guid tenantId = folderInfos[0].TenantId!.Value;

        // Step 2 — union of self+ancestor paths across the page.
        Dictionary<Guid, List<string>> folderPaths = new(folderInfos.Count);
        HashSet<string> uniquePaths = [];
        foreach (var info in folderInfos)
        {
            List<string> paths = ExpandSelfAndAncestorPaths(info.Path);
            folderPaths[info.Id] = paths;
            foreach (string p in paths)
            {
                uniquePaths.Add(p);
            }
        }

        // Step 3 — resolve all unique paths to folder ids in one query.
        Dictionary<string, Guid> pathToFolderId = uniquePaths.Count == 0
            ? []
            : await context.Folders
                .Where(f => f.TenantId == tenantId && uniquePaths.Contains(f.Path))
                .Select(f => new { f.Id, f.Path })
                .ToDictionaryAsync(x => x.Path, x => x.Id, cancellationToken)
                .ConfigureAwait(false);

        List<Guid?> allAncestorFolderIds = [.. pathToFolderId.Values.Select(id => (Guid?)id)];

        // Step 4 — folder shares matching any folder in the union of ancestor sets.
        var folderShares = allAncestorFolderIds.Count == 0
            ? []
            : await context.DocumentShares
                .Where(s => s.TenantId == tenantId
                    && granteeIds.Contains(s.GranteeId)
                    && s.TargetType == ShareTargetType.Folder
                    && allAncestorFolderIds.Contains(s.FolderId))
                .Select(s => new { s.FolderId, s.Permission, s.ExpiresAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;

        Dictionary<Guid, List<SharePermissionLevel>> folderShareIndex = [];
        foreach (var fs in folderShares)
        {
            if (fs.ExpiresAt is { } exp && exp <= now)
            {
                continue;
            }
            if (fs.FolderId is not { } fid)
            {
                continue;
            }
            if (!folderShareIndex.TryGetValue(fid, out List<SharePermissionLevel>? bucket))
            {
                bucket = [];
                folderShareIndex[fid] = bucket;
            }
            bucket.Add(fs.Permission);
        }

        // Step 5 — compose the per-folder effective permission.
        foreach (var info in folderInfos)
        {
            List<string> paths = folderPaths[info.Id];
            List<Guid> contributingFolderIds = [];
            List<SharePermissionLevel> matches = [];
            foreach (string path in paths)
            {
                if (!pathToFolderId.TryGetValue(path, out Guid fid))
                {
                    continue;
                }
                contributingFolderIds.Add(fid);
                if (folderShareIndex.TryGetValue(fid, out List<SharePermissionLevel>? bucket))
                {
                    matches.AddRange(bucket);
                }
            }

            EffectivePermissionLevel level = matches.Count == 0
                ? EffectivePermissionLevel.None
                : matches.Max() switch
                {
                    SharePermissionLevel.Manage => EffectivePermissionLevel.Manage,
                    SharePermissionLevel.Edit => EffectivePermissionLevel.Edit,
                    SharePermissionLevel.Read => EffectivePermissionLevel.Read,
                    _ => EffectivePermissionLevel.None,
                };
            output[info.Id] = new FolderPermissionResolution(level, contributingFolderIds);
        }

        return output;
    }

    /// <summary>
    /// Expands a materialised folder path into the list of self + ancestor paths (excluding
    /// the tenant root <c>"/"</c>). For <c>"/A/B/C"</c> returns <c>["/A", "/A/B", "/A/B/C"]</c>;
    /// for the tenant root itself returns an empty list.
    /// </summary>
    private static List<string> ExpandSelfAndAncestorPaths(string path)
    {
        if (path == Folder.TenantRootPath)
        {
            return [];
        }

        List<string> result = [];
        // Find each '/' starting from index 1 (skip the leading '/'). Each occurrence
        // marks the end of an ancestor's path segment.
        int separator = path.IndexOf(Folder.PathSeparator, 1, StringComparison.Ordinal);
        while (separator > 0)
        {
            result.Add(path[..separator]);
            separator = path.IndexOf(Folder.PathSeparator, separator + 1, StringComparison.Ordinal);
        }
        result.Add(path);
        return result;
    }
}
