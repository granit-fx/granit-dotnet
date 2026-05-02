namespace Granit.Documents.Endpoints.Folders.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /folders/{id}/move</c>.
/// </summary>
/// <param name="NewParentFolderId">
/// New parent folder identifier. When <c>null</c>, the folder is moved directly under
/// the invisible tenant root.
/// </param>
public sealed record MoveFolderRequest(Guid? NewParentFolderId);
