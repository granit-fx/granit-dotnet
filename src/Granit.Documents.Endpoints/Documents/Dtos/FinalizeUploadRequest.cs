namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /documents/finalize</c>.
/// </summary>
/// <param name="BlobId">Identifier returned by <c>POST /documents/upload-ticket</c>.</param>
/// <param name="FolderId">Target folder; <c>null</c> drops the document directly under the tenant root.</param>
/// <param name="Name">User-facing document name.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="CommitMessage">Optional changelog attached to the initial version.</param>
public sealed record FinalizeUploadRequest(
    Guid BlobId,
    Guid? FolderId,
    string Name,
    string? Description,
    string? CommitMessage);
