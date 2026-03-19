namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Request to generate a pre-signed download URL.
/// </summary>
/// <param name="ContainerName">Logical container the blob belongs to.</param>
/// <param name="FileName">Optional filename override for Content-Disposition.</param>
public sealed record BlobDownloadUrlRequest(
    string ContainerName,
    string? FileName = null);
