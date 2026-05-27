using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Write abstraction for tracking personal data export requests.
/// Implemented by the application's persistence layer (EF Core, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Called in two places:
/// <list type="bullet">
/// <item>By the <c>POST /privacy/export</c> endpoint to record the initial request.</item>
/// <item>By an application-side <see cref="Events.ExportCompletedEto"/> handler to mark completion.</item>
/// </list>
/// </para>
/// </remarks>
public interface IExportRequestTrackerWriter
{
    /// <summary>Records a new export request in <see cref="ExportRequestState.Pending"/> state.</summary>
    /// <param name="requestId">Saga / tracker correlation id.</param>
    /// <param name="subjectUserId">Data subject the export targets.</param>
    /// <param name="callerUserId">User who filed the request — equal to
    /// <paramref name="subjectUserId"/> for self-service, distinct for admin DSR
    /// (<c>POST /privacy/exports/on-behalf-of</c>).</param>
    /// <param name="requestedAt">Request timestamp (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordRequestAsync(
        Guid requestId,
        Guid subjectUserId,
        Guid callerUserId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Marks an existing export request as completed (fully, partially, or timed out).</summary>
    Task MarkCompletedAsync(
        Guid requestId,
        ExportRequestState state,
        BlobReference? archiveBlobReferenceId,
        IReadOnlyList<string>? missingProviders,
        CancellationToken cancellationToken = default);
}
