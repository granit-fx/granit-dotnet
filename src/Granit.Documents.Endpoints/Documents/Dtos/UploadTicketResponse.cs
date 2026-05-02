namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape response for <c>POST /documents/upload-ticket</c>.
/// </summary>
/// <param name="BlobId">Stable identifier the client passes back to <c>POST /documents/finalize</c>.</param>
/// <param name="UploadUrl">Pre-signed PUT URL valid until <see cref="ExpiresAt"/>.</param>
/// <param name="HttpMethod">HTTP method to use; always <c>"PUT"</c> for S3-compatible providers.</param>
/// <param name="ExpiresAt">UTC expiry of the pre-signed URL.</param>
/// <param name="RequiredHeaders">Headers the client must include verbatim in the PUT request (e.g. <c>Content-Type</c>).</param>
public sealed record UploadTicketResponse(
    Guid BlobId,
    Uri UploadUrl,
    string HttpMethod,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);
