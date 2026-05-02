namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /documents/{id}/versions</c> — appends a new version
/// to an existing document after the bytes have been uploaded via the
/// <c>POST /documents/upload-ticket</c> presigned PUT.
/// </summary>
/// <param name="BlobId">Identifier returned by <c>POST /documents/upload-ticket</c>.</param>
/// <param name="CommitMessage">Optional changelog attached to the new version.</param>
public sealed record AppendVersionRequest(
    Guid BlobId,
    string? CommitMessage);
