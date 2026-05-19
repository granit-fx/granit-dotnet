using Granit.DataExchange.Import.Events;
using Granit.Domain;
using Granit.Domain.ValueObjects;

namespace Granit.DataExchange.Import.Domain;

/// <summary>
/// Represents an import job with its lifecycle state and metadata.
/// </summary>
/// <remarks>
/// Inherits <see cref="AuditedAggregateRoot"/> for ISO 27001-compliant audit trail
/// (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy).
/// </remarks>
public sealed class ImportJob : AuditedAggregateRoot, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private ImportJob() { }

    /// <summary>
    /// Creates a new <see cref="ImportJob"/> in <see cref="ImportJobStatus.Created"/> state.
    /// </summary>
    public static ImportJob Create(
        Guid id,
        string definitionName,
        string entityTypeName,
        string originalFileName,
        string mimeType,
        long fileSizeBytes,
        BlobReference blobReference,
        Guid? tenantId = null) => new()
        {
            Id = id,
            DefinitionName = definitionName,
            EntityTypeName = entityTypeName,
            OriginalFileName = originalFileName,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            BlobReference = blobReference,
            Status = ImportJobStatus.Created,
            TenantId = tenantId,
        };

    /// <summary>
    /// The import definition name (e.g. <c>"Acme.PatientImport"</c>).
    /// Links to the registered <c>ImportDefinition&lt;T&gt;</c>.
    /// </summary>
    public string DefinitionName { get; private set; } = string.Empty;

    /// <summary>
    /// CLR type name of the target entity (e.g. <c>"Patient"</c>).
    /// </summary>
    public string EntityTypeName { get; private set; } = string.Empty;

    /// <summary>
    /// Original file name as uploaded by the user.
    /// </summary>
    public string OriginalFileName { get; private set; } = string.Empty;

    /// <summary>
    /// MIME type of the uploaded file (e.g. <c>"text/csv"</c>).
    /// </summary>
    public string MimeType { get; private set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Reference to the file in blob storage.
    /// </summary>
    public BlobReference BlobReference { get; private set; } = null!;

    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public ImportJobStatus Status { get; private set; } = ImportJobStatus.Created;

    /// <summary>
    /// Serialized column mappings (JSON). Set after user confirmation.
    /// </summary>
    public string? MappingsJson { get; private set; }

    /// <summary>
    /// Serialized import report (JSON). Set after execution completes.
    /// </summary>
    public string? ReportJson { get; private set; }

    /// <summary>
    /// Timestamp when the import completed (success, partial, or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Tenant identifier. Soft dependency on <c>ICurrentTenant</c>.
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>
    /// Sets the column mappings after user confirmation.
    /// </summary>
    internal void SetMappings(string mappingsJson) =>
        MappingsJson = mappingsJson;

    /// <summary>
    /// Transitions to <see cref="ImportJobStatus.Previewed"/> after header extraction.
    /// </summary>
    internal void MarkAsPreviewed()
    {
        if (Status is not ImportJobStatus.Created)
        {
            throw new InvalidOperationException($"Cannot transition to '{ImportJobStatus.Previewed}' from '{Status}'.");
        }

        Status = ImportJobStatus.Previewed;
    }

    /// <summary>
    /// Confirms mappings and transitions to <see cref="ImportJobStatus.Mapped"/>.
    /// </summary>
    internal void ConfirmMappings(string mappingsJson)
    {
        if (Status is not ImportJobStatus.Previewed)
        {
            throw new InvalidOperationException($"Cannot transition to '{ImportJobStatus.Mapped}' from '{Status}'.");
        }

        MappingsJson = mappingsJson;
        Status = ImportJobStatus.Mapped;
    }

    /// <summary>
    /// Cancels the import job.
    /// </summary>
    internal void Cancel()
    {
        if (Status is not (ImportJobStatus.Created or ImportJobStatus.Previewed or ImportJobStatus.Mapped))
        {
            throw new InvalidOperationException($"Cannot transition to '{ImportJobStatus.Cancelled}' from '{Status}'.");
        }

        Status = ImportJobStatus.Cancelled;
        AddDomainEvent(new ImportJobCancelledEvent(Id, DefinitionName));
    }

    /// <summary>
    /// Transitions to <see cref="ImportJobStatus.Executing"/>.
    /// </summary>
    internal void MarkAsExecuting()
    {
        if (Status is not ImportJobStatus.Mapped)
        {
            throw new InvalidOperationException($"Cannot transition to '{ImportJobStatus.Executing}' from '{Status}'.");
        }

        Status = ImportJobStatus.Executing;
    }

    /// <summary>
    /// Marks the import as completed with a final status and report.
    /// </summary>
    internal void Complete(ImportJobStatus finalStatus, string reportJson, DateTimeOffset completedAt)
    {
        if (Status is not ImportJobStatus.Executing)
        {
            throw new InvalidOperationException($"Cannot transition to '{finalStatus}' from '{Status}'.");
        }

        Status = finalStatus;
        ReportJson = reportJson;
        CompletedAt = completedAt;
    }
}
