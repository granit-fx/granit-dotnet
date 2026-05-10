namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape representation of a folder.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="ParentFolderId">
/// Parent folder identifier; <c>null</c> only when the folder is directly under the
/// invisible tenant root (the tenant root itself is never returned).
/// </param>
/// <param name="Name">User-facing name.</param>
/// <param name="Path">Materialised path from the tenant root, e.g. <c>"/Contracts/2026"</c>.</param>
/// <param name="Depth">Depth in the tenant tree. <c>1</c> for direct children of the tenant root.</param>
/// <param name="OwnerUserId">Identifier of the user who owns the folder.</param>
/// <param name="Status">Lifecycle status (<c>Active</c> or <c>Trashed</c>).</param>
/// <param name="TrashedAt">UTC instant the folder was trashed; <c>null</c> while active.</param>
/// <param name="Permission">
/// Effective permission the calling principal holds on the folder, resolved via the share
/// ACL (F6.5b). One of <c>None</c> / <c>Read</c> / <c>Edit</c> / <c>Manage</c>. <c>null</c>
/// when the caller did not request permission resolution.
/// </param>
public sealed record FolderResponse(
    Guid Id,
    Guid? ParentFolderId,
    string Name,
    string Path,
    int Depth,
    Guid OwnerUserId,
    string Status,
    DateTimeOffset? TrashedAt,
    string? Permission = null);
