namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Response after initiating a direct-to-cloud upload — contains the pre-signed URL.
/// </summary>
public sealed record BlobUploadInitiateResponse(
    Guid BlobId,
    string UploadUrl,
    string HttpMethod,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);
