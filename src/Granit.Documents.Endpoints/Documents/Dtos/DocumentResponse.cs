namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>Wire-shape representation of a document.</summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="FolderId">Folder identifier the document currently lives in.</param>
/// <param name="Name">User-facing display name.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="OwnerUserId">Identifier of the user who owns the document.</param>
/// <param name="CurrentVersionId">Identifier of the active <c>DocumentVersion</c>; <c>null</c> if none yet.</param>
/// <param name="Status">Lifecycle status as a string (<c>Active</c> / <c>Trashed</c> / <c>PermanentlyDeleted</c>).</param>
/// <param name="TrashedAt">UTC instant the document was trashed; <c>null</c> while active.</param>
public sealed record DocumentResponse(
    Guid Id,
    Guid FolderId,
    string Name,
    string? Description,
    Guid OwnerUserId,
    Guid? CurrentVersionId,
    string Status,
    DateTimeOffset? TrashedAt);
