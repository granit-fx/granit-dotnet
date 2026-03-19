using Granit.BlobStorage.Domain;

namespace Granit.BlobStorage;

/// <summary>
/// Outcome of <see cref="IBlobStorage.ConfirmUploadAsync"/>.
/// </summary>
/// <param name="IsValid">Whether the blob passed all validators.</param>
/// <param name="Status">Final <see cref="BlobStatus"/> after confirmation.</param>
/// <param name="VerifiedContentType">Content-Type confirmed by magic-bytes analysis; <c>null</c> if rejected.</param>
/// <param name="SizeBytes">Actual file size in bytes; <c>null</c> if rejected before size check.</param>
/// <param name="RejectionReason">Human-readable failure reason; <c>null</c> when <paramref name="IsValid"/> is <c>true</c>.</param>
public sealed record BlobConfirmationResult(
    bool IsValid,
    BlobStatus Status,
    string? VerifiedContentType,
    long? SizeBytes,
    string? RejectionReason);
