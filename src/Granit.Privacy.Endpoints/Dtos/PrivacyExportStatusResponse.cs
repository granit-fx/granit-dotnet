using Granit.Domain.ValueObjects;

namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Status of a personal data export request (GDPR Art. 15/20).
/// </summary>
/// <param name="RequestId">Correlation ID of the export saga.</param>
/// <param name="State">Current state: Pending, Completed, PartiallyCompleted, or TimedOut.</param>
/// <param name="RequestedAt">Timestamp when the export was requested (UTC).</param>
/// <param name="CompletedAt">Timestamp when the export completed (UTC), or <c>null</c> if still pending.</param>
/// <param name="ArchiveBlobReferenceId">
/// Blob reference ID of the export archive. Use the BlobStorage download endpoint
/// (<c>POST /api/v1/blobs/.../download-url</c>) to obtain a pre-signed URL.
/// </param>
/// <param name="MissingProviders">Data providers that did not respond before timeout (empty if fully completed).</param>
public sealed record PrivacyExportStatusResponse(
    Guid RequestId,
    string State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    BlobReference? ArchiveBlobReferenceId,
    IReadOnlyList<string> MissingProviders);
