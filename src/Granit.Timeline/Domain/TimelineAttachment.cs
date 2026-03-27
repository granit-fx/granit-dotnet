using Granit.Domain;

namespace Granit.Timeline.Domain;

/// <summary>
/// Links a <see cref="TimelineEntry"/> to a blob stored in Granit.BlobStorage.
/// Soft dependency: attachments only work when BlobStorage is registered.
/// The <see cref="BlobId"/> is an opaque reference (no foreign key).
/// </summary>
public sealed class TimelineAttachment : CreationAuditedEntity, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private TimelineAttachment() { }

    /// <summary>Creates a new <see cref="TimelineAttachment"/>.</summary>
    public static TimelineAttachment Create(
        Guid id,
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt,
        string createdBy,
        Guid? tenantId = null) => new()
        {
            Id = id,
            EntryId = entryId,
            BlobId = blobId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            TenantId = tenantId,
        };

    /// <summary>FK to the parent timeline entry.</summary>
    public Guid EntryId { get; private set; }

    /// <summary>BlobDescriptor.Id from Granit.BlobStorage (opaque, no FK).</summary>
    public Guid BlobId { get; private set; }

    /// <summary>Original filename (denormalized for display).</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Content type / MIME type (denormalized for display).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>File size in bytes (denormalized for display).</summary>
    public long SizeBytes { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }
}
