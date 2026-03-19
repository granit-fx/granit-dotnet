namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Request to initiate a direct-to-cloud upload.
/// </summary>
/// <param name="ContainerName">Logical container (e.g. <c>medical-images</c>).</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME content type.</param>
/// <param name="SizeBytes">Declared file size in bytes.</param>
public sealed record BlobUploadInitiateRequest(
    string ContainerName,
    string FileName,
    string ContentType,
    long SizeBytes);
