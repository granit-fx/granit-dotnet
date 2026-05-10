using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Granit.Documents.Authorization;

namespace Granit.Documents.EntityFrameworkCore.Authorization;

/// <summary>
/// Centralised cache-key and tag construction for the F6.3 effective-ACL cache layer.
/// </summary>
/// <remarks>
/// <para>
/// Keys are tenant-scoped automatically by the framework's
/// <c>TenantAwareFusionCache</c> decorator — these helpers therefore compose the
/// tenant-relative segment only and never bake the tenant id into the string.
/// </para>
/// <para>
/// Tags drive <c>RemoveByTagAsync</c> invalidation and follow the ADR-052 scheme:
/// <list type="bullet">
///   <item><c>acl:doc:{documentId}</c> — invalidated on a direct document share change.</item>
///   <item><c>acl:folder:{folderId}</c> — invalidated on a folder share change. Cache
///     entries attach this tag for the document's folder AND every ancestor folder, so
///     a grant change on any ancestor invalidates the matching entries.</item>
///   <item><c>acl:all</c> — invalidated when the folder hierarchy changes (move / rename) —
///     too cheap to walk and tag descendants individually for an event that is rare.</item>
/// </list>
/// </para>
/// </remarks>
internal static class AclCacheKeys
{
    /// <summary>
    /// Cache-key segment for "principal P resolves on document D". The grantee-set hash
    /// disambiguates principals that share <see cref="DocumentPrincipal.UserId"/> but hold
    /// different roles or groups.
    /// </summary>
    public static string Document(Guid documentId, DocumentPrincipal principal) =>
        $"acl:doc:{documentId.ToString("N", CultureInfo.InvariantCulture)}"
        + $":user:{principal.UserId.ToString("N", CultureInfo.InvariantCulture)}"
        + $":{HashGranteeSet(principal)}";

    /// <summary>
    /// Stable, allocation-light hash of the principal's deduplicated grantee set.
    /// </summary>
    /// <remarks>
    /// Sorted SHA-256 ensures two principals with the same effective grant set produce the
    /// same hash regardless of role / group iteration order. Truncated to 16 hex chars —
    /// sufficient to disambiguate principals on the cache key without bloating the string.
    /// </remarks>
    public static string HashGranteeSet(DocumentPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        IReadOnlyList<Guid> ids = principal.AllGranteeIds;
        if (ids.Count == 0)
        {
            return "0";
        }

        Guid[] sorted = [.. ids];
        Array.Sort(sorted);

        // 16 bytes per Guid; avoid LINQ to keep allocations down on the hot path.
        byte[] buffer = new byte[sorted.Length * 16];
        for (int i = 0; i < sorted.Length; i++)
        {
            sorted[i].TryWriteBytes(buffer.AsSpan(i * 16));
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(buffer, hash);
        // 8 bytes → 16 hex chars; collisions are astronomically unlikely within a single
        // tenant's principal namespace and keep the cache key compact.
        return Convert.ToHexString(hash[..8]);
    }

    /// <summary>
    /// Cache-key segment for "principal P resolves on folder F" (F6.5b). Distinct prefix
    /// from <see cref="Document"/> so a folder and a document with the same id can never
    /// collide.
    /// </summary>
    public static string Folder(Guid folderId, DocumentPrincipal principal) =>
        $"acl:folder-perm:{folderId.ToString("N", CultureInfo.InvariantCulture)}"
        + $":user:{principal.UserId.ToString("N", CultureInfo.InvariantCulture)}"
        + $":{HashGranteeSet(principal)}";

    /// <summary>
    /// Builds the tag set attached to a cached folder-permission entry: the target folder
    /// and every ancestor that participated in the path-prefix scan, plus the tenant-wide
    /// tag for bulk wipes. Mirrors <see cref="BuildEntryTags(Guid, IReadOnlyList{Guid})"/>
    /// without the <c>acl:doc:</c> tag (folder entries don't depend on a document id).
    /// </summary>
    public static string[] BuildFolderEntryTags(IReadOnlyList<Guid> folderIds)
    {
        ArgumentNullException.ThrowIfNull(folderIds);

        string[] tags = new string[folderIds.Count + 1];
        int i = 0;
        foreach (Guid folderId in folderIds)
        {
            tags[i++] = FolderTag(folderId);
        }
        tags[i] = AllTag;
        return tags;
    }

    /// <summary>Tag attached to entries resolved against a specific document.</summary>
    public static string DocumentTag(Guid documentId) =>
        $"acl:doc:{documentId.ToString("N", CultureInfo.InvariantCulture)}";

    /// <summary>Tag attached to entries that depended on a folder grant or path inclusion.</summary>
    public static string FolderTag(Guid folderId) =>
        $"acl:folder:{folderId.ToString("N", CultureInfo.InvariantCulture)}";

    /// <summary>Tenant-wide ACL tag — set on every entry for cheap mass invalidation.</summary>
    public const string AllTag = "acl:all";

    /// <summary>
    /// Builds the tag set attached to a cached entry for the given document and the
    /// folder ids participating in its resolution (the document's folder + all ancestor
    /// folders contributing to the path-prefix scan).
    /// </summary>
    public static string[] BuildEntryTags(Guid documentId, IReadOnlyList<Guid> folderIds)
    {
        ArgumentNullException.ThrowIfNull(folderIds);

        // documentId tag + each folder tag + the all-tag.
        string[] tags = new string[folderIds.Count + 2];
        int i = 0;
        tags[i++] = DocumentTag(documentId);
        foreach (Guid folderId in folderIds)
        {
            tags[i++] = FolderTag(folderId);
        }
        tags[i] = AllTag;
        return tags;
    }
}
