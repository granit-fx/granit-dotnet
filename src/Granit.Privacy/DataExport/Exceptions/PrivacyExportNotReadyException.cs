namespace Granit.Privacy.DataExport.Exceptions;

/// <summary>
/// Thrown by <see cref="IPrivacyExportDownloadResolver"/> when a download is
/// requested for an export that hasn't completed yet (no manifest reference on
/// the tracker, or the manifest blob is unreadable).
/// </summary>
/// <remarks>
/// Distinct from a 404 — the request exists, it just isn't downloadable. The
/// endpoint surface maps this to <c>409 Conflict</c> so clients can poll the
/// status endpoint instead of treating the request as gone.
/// </remarks>
public sealed class PrivacyExportNotReadyException(Guid requestId)
    : Exception($"Privacy export {requestId} is not ready for download.")
{
    /// <summary>Saga / export-request correlation id.</summary>
    public Guid RequestId { get; } = requestId;
}
