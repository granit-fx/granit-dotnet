namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /folders</c>.
/// </summary>
/// <param name="ParentFolderId">
/// Parent folder identifier. When <c>null</c>, the folder is created directly under the
/// invisible tenant root (auto-bootstrapped on first access).
/// </param>
/// <param name="Name">
/// User-facing folder name. Validated against <see cref="Domain.Folder.MaxNameLength"/>
/// and rejected when it contains the path separator <c>"/"</c>.
/// </param>
public sealed record CreateFolderRequest(Guid? ParentFolderId, string Name);
