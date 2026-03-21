namespace Granit.Privacy.DataExport;

/// <summary>
/// Read model for a GDPR export request, used by endpoints to report progress.
/// </summary>
/// <param name="RequestId">Correlation ID of the export saga.</param>
/// <param name="UserId">Identifier of the data subject.</param>
/// <param name="State">Current state of the export request.</param>
/// <param name="RequestedAt">Timestamp when the export was requested (UTC).</param>
/// <param name="CompletedAt">Timestamp when the saga completed (UTC), or <c>null</c> if still pending.</param>
/// <param name="ArchiveBlobReferenceId">
/// Blob reference ID of the completed archive (convention: <c>gdpr-export/{RequestId}</c>).
/// Use the BlobStorage download endpoint to obtain a pre-signed URL.
/// </param>
/// <param name="MissingProviders">Providers that did not respond before timeout (empty if fully completed).</param>
public sealed record ExportRequestStatus(
    Guid RequestId,
    Guid UserId,
    ExportRequestState State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? ArchiveBlobReferenceId,
    IReadOnlyList<string> MissingProviders);
