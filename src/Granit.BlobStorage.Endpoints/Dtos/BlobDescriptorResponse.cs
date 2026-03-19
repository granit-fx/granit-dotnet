using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="BlobDescriptor"/> for API responses.
/// </summary>
public sealed record BlobDescriptorResponse(
    Guid Id,
    string ContainerName,
    string OriginalFileName,
    string DeclaredContentType,
    string? VerifiedContentType,
    long DeclaredSizeBytes,
    long? ActualSizeBytes,
    BlobStatus Status,
    string? RejectionReason,
    string? DeletionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? DeletedAt);
