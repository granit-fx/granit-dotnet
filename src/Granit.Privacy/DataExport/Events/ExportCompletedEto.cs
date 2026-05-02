using Granit.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published when the privacy export Saga completes (all fragments received or timeout).
/// </summary>
/// <param name="RequestId">Correlation id of the originating export request.</param>
/// <param name="UserId">Data subject whose data was exported.</param>
/// <param name="ArchiveBlobReferenceId">
/// Logical path under which the assembled archive will live (convention:
/// <c>personal-data-export/{RequestId}</c>). The <c>Granit.Privacy.BackgroundJobs</c>
/// archive assembler uploads the actual ZIP and records the resulting blob id on the tracker.
/// </param>
/// <param name="IsPartial"><c>true</c> when the saga timed out before every provider responded.</param>
/// <param name="MissingProviders">Providers that never produced a fragment (timeout path).</param>
/// <param name="Fragments">
/// Fragments received from providers. Carries <c>BlobReferenceId</c> only — never raw data
/// (ISO 27001). Includes <c>empty:</c>-prefixed entries for providers that had no data; the
/// archive assembler records those in the manifest and skips the download.
/// </param>
/// <param name="Regulation">Privacy regulation code the request was filed under (e.g. <c>EU_GDPR</c>).</param>
/// <param name="RequestedAt">When the data subject filed the request — recorded in the archive manifest.</param>
public sealed record ExportCompletedEto(
    Guid RequestId,
    Guid UserId,
    BlobReference ArchiveBlobReferenceId,
    bool IsPartial,
    IReadOnlyList<string> MissingProviders,
    IReadOnlyList<ReceivedFragment> Fragments,
    string Regulation,
    DateTimeOffset RequestedAt) : IIntegrationEvent;
