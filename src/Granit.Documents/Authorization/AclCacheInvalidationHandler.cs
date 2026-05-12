using Granit.Documents.Domain;
using Granit.Documents.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Documents.Authorization;

/// <summary>
/// Wolverine message handlers that invalidate the F6.3 effective-ACL cache when share
/// grants change or the folder hierarchy moves.
/// </summary>
/// <remarks>
/// <para>
/// Discovered by Wolverine's handler scanning convention: public class with
/// <c>public static</c> <c>HandleAsync</c> methods. The cache is the tenant-aware
/// <see cref="IFusionCache"/> singleton — tag operations are auto-scoped to the current
/// tenant context, which matches the share / folder mutation that emitted the event.
/// </para>
/// <para>
/// Tag scheme:
/// <list type="bullet">
///   <item>Document share change → <c>acl:doc:{documentId}</c></item>
///   <item>Folder share change → <c>acl:folder:{folderId}</c> (cached entries attach this
///     tag for the document's folder AND every ancestor)</item>
///   <item>Folder path change (move / rename or tree-wide) → <see cref="AclCacheKeys.AllTag"/>
///     bulk wipe inside the tenant prefix. Path changes alter ancestor sets across the
///     subtree; tagging individually would require enumerating every descendant document.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class AclCacheInvalidationHandler
{
    /// <summary>Invalidates entries that depended on the share's target.</summary>
    public static Task HandleAsync(
        DocumentShareGrantedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        InvalidateForShareTargetAsync(cache, @event.TargetType, @event.FolderId, @event.DocumentId, cancellationToken);

    /// <summary>Invalidates entries that depended on the revoked share's target.</summary>
    public static Task HandleAsync(
        DocumentShareRevokedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        InvalidateForShareTargetAsync(cache, @event.TargetType, @event.FolderId, @event.DocumentId, cancellationToken);

    /// <summary>
    /// Invalidates the entire tenant ACL cache when a folder's path changes — the move /
    /// rename shifts the ancestor set of every descendant document, which can affect any
    /// cached entry that depended on a folder share in the moved subtree.
    /// </summary>
    public static Task HandleAsync(
        FolderPathChangedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(AclCacheKeys.AllTag, token: cancellationToken).AsTask();

    /// <summary>
    /// Invalidates the entire tenant ACL cache when an entire folder subtree is
    /// re-pathed (F2.4 bulk move). Tree-level events arrive once per move, even when
    /// hundreds of descendants shift.
    /// </summary>
    public static Task HandleAsync(
        FolderTreePathChangedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(AclCacheKeys.AllTag, token: cancellationToken).AsTask();

    private static Task InvalidateForShareTargetAsync(
        IFusionCache cache,
        ShareTargetType targetType,
        Guid? folderId,
        Guid? documentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return targetType switch
        {
            ShareTargetType.Folder when folderId is { } fid =>
                cache.RemoveByTagAsync(AclCacheKeys.FolderTag(fid), token: cancellationToken).AsTask(),
            ShareTargetType.Document when documentId is { } did =>
                cache.RemoveByTagAsync(AclCacheKeys.DocumentTag(did), token: cancellationToken).AsTask(),
            _ => Task.CompletedTask,
        };
    }
}
