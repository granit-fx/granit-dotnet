using Granit.BlobStorage.Events;
using Granit.Core.Domain;

namespace Granit.BlobStorage.Domain;

/// <summary>
/// Relational record tracking the lifecycle of a cloud-stored object.
/// </summary>
/// <remarks>
/// <para>
/// Persisted by <see cref="IBlobDescriptorWriter"/>. The binary content lives on S3;
/// this entity is the authoritative source of truth for status and audit metadata.
/// </para>
/// <para>
/// Legal state transitions:
/// <c>Pending -> Uploading -> Valid -> Deleted</c> or
/// <c>Pending -> Uploading -> Rejected</c>.
/// </para>
/// <para>
/// RGPD /ISO 27001: records are <b>never deleted from the database</b>.
/// <see cref="BlobStatus.Deleted"/> means the S3 bytes are gone; the audit row remains for 3 years.
/// </para>
/// </remarks>
public sealed class BlobDescriptor : AggregateRoot, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private BlobDescriptor() { }

    /// <summary>
    /// Creates a new <see cref="BlobDescriptor"/> in <see cref="BlobStatus.Pending"/> state.
    /// </summary>
    public static BlobDescriptor Create(
        Guid id,
        Guid? tenantId,
        string containerName,
        string objectKey,
        BlobUploadRequest request,
        DateTimeOffset createdAt) => new()
        {
            Id = id,
            TenantId = tenantId,
            ContainerName = containerName,
            ObjectKey = objectKey,
            OriginalFileName = request.FileName,
            DeclaredContentType = request.ContentType,
            MaxAllowedBytes = request.MaxAllowedBytes,
            Status = BlobStatus.Pending,
            CreatedAt = createdAt,
        };

    /// <summary>Identifier of the tenant that owns this blob.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Explicit implementation preserves the <c>private set</c> DDD encapsulation
    /// on the public property while satisfying the interface contract.
    /// Used by <c>AuditedEntityInterceptor</c> to inject the tenant identifier.
    /// </remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Logical container (e.g. <c>medical-images</c>, <c>prescriptions</c>).</summary>
    public string ContainerName { get; private set; } = string.Empty;

    /// <summary>Full S3 object key, including the tenant prefix.</summary>
    public string ObjectKey { get; private set; } = string.Empty;

    /// <summary>Original filename as provided by the client.</summary>
    public string OriginalFileName { get; private set; } = string.Empty;

    /// <summary>Content-Type declared by the client at ticket-creation time.</summary>
    public string DeclaredContentType { get; private set; } = string.Empty;

    /// <summary>Maximum file size in bytes allowed for this upload, as declared at ticket-creation time.</summary>
    public long MaxAllowedBytes { get; private set; }

    /// <summary>Content-Type verified by <see cref="IBlobValidator"/> magic-bytes check; null until validated.</summary>
    public string? VerifiedContentType { get; private set; }

    /// <summary>Actual size in bytes as read from S3 HEAD; null until validated.</summary>
    public long? SizeBytes { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public BlobStatus Status { get; private set; }

    /// <summary>UTC instant when the upload ticket was issued.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC instant when the blob passed all validators; null until <see cref="BlobStatus.Valid"/>.</summary>
    public DateTimeOffset? ValidatedAt { get; private set; }

    /// <summary>UTC instant when the S3 object was physically deleted; null until <see cref="BlobStatus.Deleted"/>.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Human-readable reason for rejection; null unless <see cref="BlobStatus.Rejected"/>.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Human-readable reason for deletion (e.g. "RGPD Art. 17 request"); null unless <see cref="BlobStatus.Deleted"/>.</summary>
    public string? DeletionReason { get; private set; }

    /// <summary>
    /// Transitions from <see cref="BlobStatus.Pending"/> to <see cref="BlobStatus.Uploading"/>.
    /// Called when the S3 upload notification is received.
    /// </summary>
    /// <exception cref="InvalidOperationException">When current status is not <see cref="BlobStatus.Pending"/>.</exception>
    public void MarkAsUploading()
    {
        if (Status != BlobStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot transition BlobDescriptor {Id} from {Status} to {BlobStatus.Uploading}.");
        }

        Status = BlobStatus.Uploading;
    }

    /// <summary>
    /// Transitions from <see cref="BlobStatus.Uploading"/> to <see cref="BlobStatus.Valid"/>.
    /// Called when all <see cref="IBlobValidator"/> instances pass.
    /// </summary>
    /// <param name="verifiedContentType">Content-Type confirmed by magic-bytes analysis.</param>
    /// <param name="sizeBytes">Actual size read from S3 HEAD.</param>
    /// <param name="validatedAt">UTC instant of validation.</param>
    /// <exception cref="InvalidOperationException">When current status is not <see cref="BlobStatus.Uploading"/>.</exception>
    public void MarkAsValid(string verifiedContentType, long sizeBytes, DateTimeOffset validatedAt)
    {
        if (Status != BlobStatus.Uploading)
        {
            throw new InvalidOperationException(
                $"Cannot transition BlobDescriptor {Id} from {Status} to {BlobStatus.Valid}.");
        }

        Status = BlobStatus.Valid;
        VerifiedContentType = verifiedContentType;
        SizeBytes = sizeBytes;
        ValidatedAt = validatedAt;

        AddDomainEvent(new BlobValidated(Id, ContainerName, verifiedContentType, sizeBytes));
    }

    /// <summary>
    /// Transitions from <see cref="BlobStatus.Uploading"/> to <see cref="BlobStatus.Rejected"/>.
    /// The S3 object must be physically deleted by the caller before invoking this method.
    /// </summary>
    /// <param name="reason">Human-readable rejection reason for the audit trail.</param>
    /// <exception cref="InvalidOperationException">When current status is not <see cref="BlobStatus.Uploading"/>.</exception>
    public void MarkAsRejected(string reason)
    {
        if (Status != BlobStatus.Uploading)
        {
            throw new InvalidOperationException(
                $"Cannot transition BlobDescriptor {Id} from {Status} to {BlobStatus.Rejected}.");
        }

        Status = BlobStatus.Rejected;
        RejectionReason = reason;

        AddDomainEvent(new BlobRejected(Id, ContainerName, reason));
    }

    /// <summary>
    /// Transitions from <see cref="BlobStatus.Valid"/> to <see cref="BlobStatus.Deleted"/>.
    /// The S3 object must be physically deleted by the caller before invoking this method.
    /// The record is retained in the database for ISO 27001 audit compliance.
    /// </summary>
    /// <param name="deletedAt">UTC instant of deletion.</param>
    /// <param name="reason">Optional human-readable reason (e.g. "RGPD Art. 17 erasure request").</param>
    /// <exception cref="InvalidOperationException">When current status is not <see cref="BlobStatus.Valid"/>.</exception>
    public void MarkAsDeleted(DateTimeOffset deletedAt, string? reason = null)
    {
        if (Status != BlobStatus.Valid)
        {
            throw new InvalidOperationException(
                $"Cannot transition BlobDescriptor {Id} from {Status} to {BlobStatus.Deleted}.");
        }

        Status = BlobStatus.Deleted;
        DeletedAt = deletedAt;
        DeletionReason = reason;

        AddDomainEvent(new BlobDeleted(Id, ContainerName, reason));
    }
}
