namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /documents/{id}/move</c>.
/// </summary>
/// <param name="NewFolderId">
/// New folder identifier. When <c>null</c>, the document is moved directly under the
/// invisible tenant root.
/// </param>
public sealed record MoveDocumentRequest(Guid? NewFolderId);
