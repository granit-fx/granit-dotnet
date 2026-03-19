using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Response after confirming an upload — includes validation outcome.
/// </summary>
public sealed record BlobConfirmUploadResponse(
    Guid BlobId,
    bool IsValid,
    BlobStatus Status,
    string? VerifiedContentType,
    long? SizeBytes,
    string? RejectionReason);
