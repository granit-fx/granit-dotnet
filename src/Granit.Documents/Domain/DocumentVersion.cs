using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Domain;

/// <summary>
/// Immutable version of a <see cref="Document"/>'s content. One row per upload (or per
/// restore-from-history operation in F4.3); the bytes themselves live in
/// <c>Granit.BlobStorage</c> under <see cref="BlobDescriptorId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Versions are append-only. The <see cref="Document.CurrentVersionId"/> pointer moves
/// to the newest version on each finalised upload (F3.2) or to a re-promoted older one
/// on restore (F4.3) — the historical rows are never mutated.
/// </para>
/// <para>
/// <see cref="VersionNumber"/> is monotonic per document, starting at <c>1</c>. F4.1
/// computes <c>VersionNumber = max + 1</c> when finalising re-uploads; F3.2 always
/// creates a v1 row when the parent document is brand-new.
/// </para>
/// </remarks>
public sealed class DocumentVersion : Entity, IMultiTenant
{
    /// <summary>Maximum length, in characters, of an optional <see cref="CommitMessage"/>.</summary>
    public const int MaxCommitMessageLength = 1000;

    /// <summary>Parameterless constructor required by the EF Core materialiser.</summary>
    private DocumentVersion() { }

    /// <summary>
    /// Creates a new version row. Internal: the only legitimate caller is
    /// <c>FolderService</c> / <c>DocumentService</c> from <c>Granit.Documents.EntityFrameworkCore</c>;
    /// HTTP and other consumers go through <see cref="Document"/>.
    /// </summary>
    internal static DocumentVersion Create(
        Guid id,
        Document document,
        int versionNumber,
        Guid blobDescriptorId,
        long sizeBytes,
        string contentType,
        string? contentHash,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt,
        string? commitMessage = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be 1 or greater.");
        }
        if (blobDescriptorId == Guid.Empty)
        {
            throw new ArgumentException("Blob descriptor id cannot be empty.", nameof(blobDescriptorId));
        }
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size must be non-negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (commitMessage is { Length: > MaxCommitMessageLength })
        {
            throw new ArgumentException(
                $"Commit message exceeds {MaxCommitMessageLength} characters.",
                nameof(commitMessage));
        }

        return new DocumentVersion
        {
            Id = id,
            TenantId = document.TenantId,
            DocumentId = document.Id,
            VersionNumber = versionNumber,
            BlobDescriptorId = blobDescriptorId,
            SizeBytes = sizeBytes,
            ContentType = contentType,
            ContentHash = contentHash,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt,
            CommitMessage = commitMessage,
        };
    }

    /// <summary>Identifier of the tenant this version belongs to (mirrors the parent <see cref="Document.TenantId"/>).</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>Explicit interface implementation — see <see cref="Document"/> for rationale.</remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Parent document identifier.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Monotonic version number within the parent document; starts at 1.</summary>
    public int VersionNumber { get; private set; }

    /// <summary>FK to <c>Granit.BlobStorage</c> — the actual content lives there.</summary>
    public Guid BlobDescriptorId { get; private set; }

    /// <summary>Verified size in bytes (from <c>BlobStorage</c>'s post-upload validation).</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Verified content type (from <c>BlobStorage</c>'s magic-bytes check).</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Optional content hash for tamper detection / dedup awareness.</summary>
    public string? ContentHash { get; private set; }

    /// <summary>User who finalised this version.</summary>
    public Guid UploadedByUserId { get; private set; }

    /// <summary>UTC instant the version was finalised.</summary>
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Optional free-text changelog supplied by the uploader.</summary>
    public string? CommitMessage { get; private set; }
}
