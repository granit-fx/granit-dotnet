using Granit.Documents.Renditions.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Renditions.Domain;

/// <summary>
/// Aggregate root representing a single rendition of a <see cref="Granit.Documents.Domain.DocumentVersion"/>.
/// </summary>
/// <remarks>
/// <para>
/// Renditions are uniquely identified by <c>(DocumentVersionId, Type, Format)</c>. A new
/// <c>DocumentVersion</c> on the parent <c>Document</c> invalidates and supersedes the old
/// rendition set — the F16.4 background job regenerates them on the next
/// <c>DocumentVersionAddedEvent</c>.
/// </para>
/// <para>
/// The bytes themselves live in <c>Granit.BlobStorage</c> under
/// <see cref="BlobDescriptorId"/>; permanent-delete of the parent <c>Document</c>
/// cascades on every rendition (each blob soft-deletes via
/// <c>IBlobStorage.DeleteAsync</c>) and decrements
/// <c>TenantStorageQuota.RenditionUsageBytes</c>.
/// </para>
/// </remarks>
public sealed class DocumentRendition : AggregateRoot, IMultiTenant
{
    /// <summary>EF Core materialisation constructor.</summary>
    private DocumentRendition() { }

    /// <summary>
    /// Creates a <see cref="RenditionStatus.Pending"/> row before the pipeline runs. The
    /// <c>BlobDescriptorId</c> / <c>SizeBytes</c> / <c>Width</c> / <c>Height</c> columns
    /// stay <c>null</c> until <see cref="MarkReady"/> is called by the generation flow.
    /// </summary>
    public static DocumentRendition CreatePending(
        Guid id,
        Guid? tenantId,
        Guid documentId,
        Guid documentVersionId,
        RenditionType type,
        string format,
        DateTimeOffset now)
    {
        ValidateFormat(format);
        return new DocumentRendition
        {
            Id = id,
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            GeneratedFromVersionId = documentVersionId,
            Type = type,
            Format = format,
            Status = RenditionStatus.Pending,
            CreatedAt = now,
        };
    }

    /// <summary>Identifier of the tenant the parent document belongs to.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>Explicit interface implementation — see <c>Document</c> for rationale.</remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Parent document identifier (denormalised for fast lookup; matches <c>DocumentVersion.DocumentId</c>).</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>The version this rendition projects from.</summary>
    public Guid DocumentVersionId { get; private set; }

    /// <summary>
    /// The version that drove the generation — same as <see cref="DocumentVersionId"/> at creation
    /// time, but kept explicit to support background regeneration after a version bump.
    /// </summary>
    public Guid GeneratedFromVersionId { get; private set; }

    /// <summary>Kind of rendition (<see cref="RenditionType.Thumbnail"/>, <see cref="RenditionType.Web"/>, …).</summary>
    public RenditionType Type { get; private set; }

    /// <summary>
    /// Output format token — e.g. <c>"image/png"</c>, <c>"image/webp"</c>, <c>"image/jpeg"</c>,
    /// <c>"video/mp4"</c>. Lower-case MIME convention so equality is straightforward.
    /// </summary>
    public string Format { get; private set; } = string.Empty;

    /// <summary>Lifecycle status — see <see cref="RenditionStatus"/>.</summary>
    public RenditionStatus Status { get; private set; }

    /// <summary><c>BlobDescriptorId</c> in <c>Granit.BlobStorage</c>; <c>null</c> until <see cref="MarkReady"/>.</summary>
    public Guid? BlobDescriptorId { get; private set; }

    /// <summary>Persisted output size in bytes; <c>null</c> until ready.</summary>
    public long? SizeBytes { get; private set; }

    /// <summary>Output width in pixels (<c>null</c> for non-raster outputs or until ready).</summary>
    public int? Width { get; private set; }

    /// <summary>Output height in pixels.</summary>
    public int? Height { get; private set; }

    /// <summary>UTC instant the row was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC instant the pipeline finished. <c>null</c> until <see cref="MarkReady"/> / <see cref="MarkFailed"/>.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Terminal failure reason (provider chain exhausted, sandbox timeout, etc.).
    /// <c>null</c> while the rendition is <c>Ready</c> or still in progress.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>Transitions the row to <see cref="RenditionStatus.Generating"/>.</summary>
    public void MarkGenerating()
    {
        if (Status != RenditionStatus.Pending && Status != RenditionStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Rendition {Id} cannot start generating from status {Status}.");
        }
        Status = RenditionStatus.Generating;
        FailureReason = null;
    }

    /// <summary>
    /// Records a successful generation — moves to <see cref="RenditionStatus.Ready"/>,
    /// captures the output blob + size + dimensions, and emits
    /// <see cref="RenditionGeneratedEvent"/>.
    /// </summary>
    public void MarkReady(
        Guid blobDescriptorId,
        long sizeBytes,
        int? width,
        int? height,
        DateTimeOffset now)
    {
        if (Status != RenditionStatus.Generating)
        {
            throw new InvalidOperationException(
                $"Rendition {Id} cannot transition to Ready from status {Status}.");
        }
        if (blobDescriptorId == Guid.Empty)
        {
            throw new ArgumentException("Blob descriptor id cannot be empty.", nameof(blobDescriptorId));
        }
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size must be non-negative.");
        }

        BlobDescriptorId = blobDescriptorId;
        SizeBytes = sizeBytes;
        Width = width;
        Height = height;
        Status = RenditionStatus.Ready;
        CompletedAt = now;
        FailureReason = null;
        AddDomainEvent(new RenditionGeneratedEvent(
            Id, TenantId, DocumentId, DocumentVersionId, Type, Format, sizeBytes, now));
    }

    /// <summary>
    /// Records a terminal failure — moves to <see cref="RenditionStatus.Failed"/>, persists
    /// the reason, and emits <see cref="RenditionFailedEvent"/>. Callers may retry by
    /// calling <see cref="MarkGenerating"/> again, which clears the reason.
    /// </summary>
    public void MarkFailed(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = RenditionStatus.Failed;
        FailureReason = reason;
        CompletedAt = now;
        AddDomainEvent(new RenditionFailedEvent(
            Id, TenantId, DocumentId, DocumentVersionId, Type, Format, reason, now));
    }

    private static void ValidateFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        if (!format.Contains('/'))
        {
            throw new ArgumentException(
                $"Format must be a MIME type (e.g. 'image/png'); got '{format}'.", nameof(format));
        }
    }
}
