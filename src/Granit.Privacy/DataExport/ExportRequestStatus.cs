using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Read model for a personal data export request, used by endpoints to report progress.
/// </summary>
/// <param name="RequestId">Correlation ID of the export saga.</param>
/// <param name="SubjectUserId">Identifier of the data subject the archive belongs to.</param>
/// <param name="CallerUserId">User who filed the request — equal to <paramref name="SubjectUserId"/>
/// for self-service exports, distinct when the export was created via
/// <c>POST /privacy/exports/on-behalf-of</c>.</param>
/// <param name="State">Current state of the export request.</param>
/// <param name="RequestedAt">Timestamp when the export was requested (UTC).</param>
/// <param name="CompletedAt">Timestamp when the saga completed (UTC), or <c>null</c> if still pending.</param>
/// <param name="ArchiveBlobReferenceId">
/// Blob reference ID of the completed archive (convention: <c>personal-data-export/{RequestId}</c>).
/// Use the BlobStorage download endpoint to obtain a pre-signed URL.
/// </param>
/// <param name="MissingProviders">Providers that did not respond before timeout (empty if fully completed).</param>
public sealed record ExportRequestStatus(
    Guid RequestId,
    Guid SubjectUserId,
    Guid CallerUserId,
    ExportRequestState State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    BlobReference? ArchiveBlobReferenceId,
    IReadOnlyList<string> MissingProviders)
{
    /// <summary>
    /// Legacy alias preserved for backward compatibility — equal to
    /// <see cref="SubjectUserId"/>. New code should prefer
    /// <see cref="SubjectUserId"/> or <see cref="CallerUserId"/> explicitly.
    /// </summary>
    public Guid UserId => SubjectUserId;
}
