using Granit.Domain;
using Granit.Privacy.DataExport;

namespace Granit.Privacy.EntityFrameworkCore.Entities;

/// <summary>
/// Read-model row persisted by <c>EfExportRequestTracker</c> for each personal-data export
/// request. Not an aggregate root — the state machine lives in <c>PersonalDataExportSaga</c>;
/// this entity is an append-then-update CQRS projection for the <c>GET /privacy/exports</c>
/// endpoints.
/// </summary>
public sealed class ExportRequestEntity : Entity, IMultiTenant
{
    /// <summary>Data subject whose personal data was requested.</summary>
    public Guid UserId { get; set; }

    /// <summary>Current lifecycle state of the request.</summary>
    public ExportRequestState State { get; set; }

    /// <summary>UTC timestamp when the user submitted the export request.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>UTC timestamp when the archive was assembled, or <c>null</c> while pending.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Blob reference for the assembled archive once the saga completes.
    /// Convention: <c>personal-data-export/{RequestId}</c> (see
    /// <see cref="PrivacyExportContainerNames.ArchiveBlobReferenceId"/>).
    /// </summary>
    public string? ArchiveBlobReferenceId { get; set; }

    /// <summary>
    /// Providers that did not respond before the saga timeout. Empty for fully-completed
    /// requests. Serialized as a JSON string column.
    /// </summary>
    public List<string> MissingProviders { get; set; } = [];

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }
}
