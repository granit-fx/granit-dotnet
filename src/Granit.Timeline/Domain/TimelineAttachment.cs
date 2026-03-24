using Granit.Domain;

namespace Granit.Timeline.Domain;

/// <summary>
/// Links a <see cref="TimelineEntry"/> to a blob stored in Granit.BlobStorage.
/// Soft dependency: attachments only work when BlobStorage is registered.
/// The <see cref="BlobId"/> is an opaque reference (no foreign key).
/// </summary>
public sealed class TimelineAttachment : CreationAuditedEntity, IMultiTenant
{
    /// <summary>FK to the parent timeline entry.</summary>
    public Guid EntryId { get; set; }

    /// <summary>BlobDescriptor.Id from Granit.BlobStorage (opaque, no FK).</summary>
    public Guid BlobId { get; set; }

    /// <summary>Original filename (denormalized for display).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Content type / MIME type (denormalized for display).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes (denormalized for display).</summary>
    public long SizeBytes { get; set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }
}
