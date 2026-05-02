namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /documents/upload-ticket</c>.
/// </summary>
/// <param name="FileName">Original filename provided by the client (used for the blob's <c>OriginalFileName</c>).</param>
/// <param name="ContentType">MIME type declared by the client. Re-verified post-upload via magic bytes.</param>
/// <param name="MaxAllowedBytes">Maximum file size in bytes. Enforced post-upload by BlobStorage.</param>
public sealed record UploadTicketRequest(string FileName, string ContentType, long MaxAllowedBytes);
