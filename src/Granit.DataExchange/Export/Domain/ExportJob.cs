using Granit.Domain;
using Granit.Domain.ValueObjects;

namespace Granit.DataExchange.Export.Domain;

/// <summary>
/// Represents an export job with its lifecycle state and metadata.
/// </summary>
/// <remarks>
/// Inherits <see cref="AuditedAggregateRoot"/> for ISO 27001-compliant audit trail
/// (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy).
/// </remarks>
public sealed class ExportJob : AuditedAggregateRoot, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private ExportJob() { }

    /// <summary>
    /// Creates a new <see cref="ExportJob"/> in <see cref="ExportJobStatus.Queued"/> state.
    /// </summary>
    public static ExportJob Create(
        Guid id,
        string definitionName,
        string format,
        ExportRequest request,
        Guid? tenantId = null) => new()
        {
            Id = id,
            DefinitionName = definitionName,
            Format = format,
            Request = request,
            Status = ExportJobStatus.Queued,
            TenantId = tenantId,
        };

    /// <summary>
    /// The export definition name (e.g. <c>"Acme.PatientExport"</c>).
    /// Links to the registered <c>ExportDefinition&lt;T, TFilter&gt;</c>.
    /// </summary>
    public string DefinitionName { get; private set; } = string.Empty;

    /// <summary>
    /// Output format (<c>"xlsx"</c> or <c>"csv"</c>).
    /// </summary>
    public string Format { get; private set; } = string.Empty;

    /// <summary>
    /// Original <see cref="ExportRequest"/>. Persisted as a JSON-serialized value;
    /// preserved for auditability and retry.
    /// </summary>
    public ExportRequest Request { get; private set; } = null!;

    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public ExportJobStatus Status { get; private set; } = ExportJobStatus.Queued;

    /// <summary>
    /// Reference to the generated file in blob storage. Set when <see cref="Status"/> is
    /// <see cref="ExportJobStatus.Completed"/>.
    /// </summary>
    public BlobReference? BlobReference { get; private set; }

    /// <summary>
    /// Generated file name for download (e.g. <c>"patients_2026-03-03.xlsx"</c>).
    /// </summary>
    public string? FileName { get; private set; }

    /// <summary>
    /// Number of rows exported.
    /// </summary>
    public int? RowCount { get; private set; }

    /// <summary>
    /// Error message if <see cref="Status"/> is <see cref="ExportJobStatus.Failed"/>.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Timestamp when the export completed (success or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Tenant identifier. Soft dependency on <c>ICurrentTenant</c>.
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>
    /// Transitions to <see cref="ExportJobStatus.Exporting"/>.
    /// </summary>
    internal void MarkAsExporting()
    {
        if (Status is not ExportJobStatus.Queued)
        {
            throw new InvalidOperationException($"Cannot transition to '{ExportJobStatus.Exporting}' from '{Status}'.");
        }

        Status = ExportJobStatus.Exporting;
    }

    /// <summary>
    /// Marks the export as completed.
    /// </summary>
    internal void Complete(BlobReference blobReference, string fileName, int rowCount, DateTimeOffset completedAt)
    {
        if (Status is not ExportJobStatus.Exporting)
        {
            throw new InvalidOperationException($"Cannot transition to '{ExportJobStatus.Completed}' from '{Status}'.");
        }

        Status = ExportJobStatus.Completed;
        BlobReference = blobReference;
        FileName = fileName;
        RowCount = rowCount;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Marks the export as failed.
    /// </summary>
    internal void Fail(string errorMessage, DateTimeOffset completedAt)
    {
        if (Status is not ExportJobStatus.Exporting)
        {
            throw new InvalidOperationException($"Cannot transition to '{ExportJobStatus.Failed}' from '{Status}'.");
        }

        Status = ExportJobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = completedAt;
    }
}
