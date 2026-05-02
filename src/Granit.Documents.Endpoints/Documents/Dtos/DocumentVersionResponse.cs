namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>Wire-shape representation of a single <c>DocumentVersion</c>.</summary>
/// <param name="Id">Unique identifier of this version.</param>
/// <param name="DocumentId">Parent document identifier.</param>
/// <param name="VersionNumber">Monotonic version number within the parent document; starts at 1.</param>
/// <param name="BlobDescriptorId">FK to <c>Granit.BlobStorage</c> — the actual content lives there.</param>
/// <param name="SizeBytes">Verified size in bytes (from <c>BlobStorage</c>'s post-upload validation).</param>
/// <param name="ContentType">Verified content type (from <c>BlobStorage</c>'s magic-bytes check).</param>
/// <param name="ContentHash">Optional content hash for tamper detection / dedup awareness.</param>
/// <param name="UploadedByUserId">User who finalised this version.</param>
/// <param name="UploadedAt">UTC instant the version was finalised.</param>
/// <param name="CommitMessage">Optional free-text changelog supplied by the uploader.</param>
/// <param name="IsCurrent">
/// <c>true</c> when this row matches the parent document's <c>CurrentVersionId</c>
/// at the moment the page was rendered. The list endpoint computes it server-side;
/// the append endpoint always returns <c>true</c> (a freshly-appended version is
/// the new current).
/// </param>
public sealed record DocumentVersionResponse(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    Guid BlobDescriptorId,
    long SizeBytes,
    string ContentType,
    string? ContentHash,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt,
    string? CommitMessage,
    bool IsCurrent);
