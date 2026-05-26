using Granit.Domain.ValueObjects;

namespace Granit.Privacy.DataExport.Fragments;

/// <summary>
/// Base type for a single entry contributed by an <see cref="IPrivacyDataProvider"/> to the
/// Takeout-style export archive. Two concrete kinds exist: <see cref="StagedExportFragment"/>
/// for transient content uploaded to the staging container, and
/// <see cref="PassThroughExportFragment"/> for blob content already persisted elsewhere
/// (single-transit — no staging round-trip).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IntegrityTag"/> is an HMAC-SHA256 capability signed by
/// <see cref="Security.IExportHmacSigner"/> when the fragment is produced. The archive
/// assembler verifies the tag before opening the underlying blob — this closes
/// <c>VULN-001</c> (BOLA) and <c>VULN-102</c> (Wolverine outbox tampering): a fragment
/// referencing a blob the subject does not own cannot be assembled unless it carries a
/// valid signature.
/// </para>
/// <para>
/// <see cref="EntryPath"/> values are sanitized by <see cref="Sanitization.EntryPathSanitizer"/>
/// at assembly time. Providers that build paths from user-controlled data (file names,
/// folder hierarchies) MUST sanitize before yielding to keep <c>InvalidExportEntryPathException</c>
/// out of the hot path.
/// </para>
/// </remarks>
public abstract record ExportFragment
{
    /// <summary>
    /// Relative path the fragment occupies inside the export ZIP
    /// (for example <c>"Documents/2024/foo.pdf"</c>).
    /// Validated by <see cref="Sanitization.EntryPathSanitizer"/>: rejects <c>..</c>,
    /// absolute paths, control characters, Windows reserved names, and out-of-bounds lengths.
    /// </summary>
    public required string EntryPath { get; init; }

    /// <summary>MIME content type of the fragment. Drives compression mode (Store vs Optimal).</summary>
    public required string ContentType { get; init; }

    /// <summary>Best-effort byte count when known by the provider — used for sharding planning.</summary>
    public long? KnownSizeBytes { get; init; }

    /// <summary>
    /// HMAC capability tag binding the fragment to its request, subject, source blob, and
    /// entry path. Format: <c>v{N}:Base64Url(HMAC-SHA256(K_export, ...))</c>.
    /// Verified by <see cref="Security.IExportHmacSigner.Verify"/> before the assembler
    /// opens the source stream.
    /// </summary>
    public required string IntegrityTag { get; init; }
}

/// <summary>
/// Fragment whose bytes have been uploaded to the staging container as part of the
/// scatter-gather phase. JSON payloads from <c>Identity</c>, <c>Auditing</c>,
/// <c>Notifications</c> use this kind.
/// </summary>
public sealed record StagedExportFragment : ExportFragment
{
    /// <summary>Reference to the staging blob holding the fragment bytes.</summary>
    public required BlobReference StagedBlob { get; init; }
}

/// <summary>
/// Fragment whose bytes already live in another blob container (typically the source
/// content the subject owns — e.g. <c>Documents</c> binaries). The assembler streams the
/// source directly into the ZIP entry, avoiding a staging round-trip ("single-transit").
/// </summary>
public sealed record PassThroughExportFragment : ExportFragment
{
    /// <summary>Reference to the source blob owned by the subject.</summary>
    public required BlobReference SourceBlob { get; init; }
}
