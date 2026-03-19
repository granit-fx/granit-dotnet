namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Request to confirm a client-side upload has completed.
/// </summary>
/// <param name="ContainerName">Logical container the blob belongs to.</param>
public sealed record BlobConfirmUploadRequest(string ContainerName);
