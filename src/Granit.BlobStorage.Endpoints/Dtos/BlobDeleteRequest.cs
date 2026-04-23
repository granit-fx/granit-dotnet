namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Request to delete a blob.
/// </summary>
/// <param name="ContainerName">Logical container the blob belongs to.</param>
/// <param name="DeletionReason">Audit trail reason (e.g. "GDPR Art. 17 erasure").</param>
public sealed record BlobDeleteRequest(
    string ContainerName,
    string? DeletionReason = null);
