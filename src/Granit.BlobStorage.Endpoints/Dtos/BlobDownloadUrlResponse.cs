namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Response containing a pre-signed download URL.
/// </summary>
public sealed record BlobDownloadUrlResponse(
    string DownloadUrl,
    DateTimeOffset ExpiresAt);
