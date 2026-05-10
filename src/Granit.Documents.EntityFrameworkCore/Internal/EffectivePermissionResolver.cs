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
        DocumentResolutionResult result = await ResolveDocumentAsync(documentId, principal, cancellationToken)
            .ConfigureAwait(false);
        return result.Permission;
    }

    /// <summary>
    /// Resolves the effective permission AND returns the ancestor folder ids visited during
    /// resolution. The cache decorator (F6.3) uses the folder ids to tag the cached entry,
    /// so a folder share change invalidates exactly the entries that depended on it.
    /// </summary>
    /// <remarks>
    /// Internal contract — the public <see cref="IEffectivePermissionResolver"/> stays a
    /// single-value API.
    /// </remarks>
    internal async Task<DocumentResolutionResult> ResolveDocumentAsync(
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
            return new DocumentResolutionResult(EffectivePermissionLevel.None, []);
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
            return new DocumentResolutionResult(EffectivePermissionLevel.None, []);
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

        return new DocumentResolutionResult(level, ancestorFolderIds);
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
