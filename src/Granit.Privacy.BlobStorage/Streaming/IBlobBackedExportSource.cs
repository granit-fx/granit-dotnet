using Granit.Domain.ValueObjects;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Privacy.BlobStorage.Streaming;

/// <summary>
/// Helper consumed by blob-backed providers (Documents, attachments, …) that already
/// store their content in a blob container. Materialises one HMAC-signed
/// <see cref="PassThroughExportFragment"/> per source item without an intermediate
/// staging upload — the assembler streams the source straight into the export
/// archive ("single-transit").
/// </summary>
/// <remarks>
/// <para>
/// The helper performs three jobs per item:
/// <list type="bullet">
///   <item>Sanitises the supplied entry path via
///   <see cref="Granit.Privacy.DataExport.Sanitization.EntryPathSanitizer.Sanitize"/>.</item>
///   <item>Signs an HMAC capability tag binding the request, subject, source
///   container, blob id, and entry path so the assembler can refuse forged
///   fragments (closes VULN-001 / VULN-102).</item>
///   <item>Yields a <see cref="PassThroughExportFragment"/> describing the entry —
///   the source bytes themselves are read at assembly time, not here.</item>
/// </list>
/// </para>
/// <para>
/// Providers stay tiny: the typical implementation paginates its domain query into
/// <see cref="BlobBackedExportItem"/> records and forwards them to <see cref="StreamAsync"/>.
/// </para>
/// </remarks>
public interface IBlobBackedExportSource
{
    /// <summary>
    /// Lazily yields one <see cref="PassThroughExportFragment"/> per source item.
    /// Order is preserved.
    /// </summary>
    IAsyncEnumerable<ExportFragment> StreamAsync(
        IAsyncEnumerable<BlobBackedExportItem> items,
        PrivacyExportContext context,
        string providerName,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes a single source blob a provider wants represented as a pass-through
/// fragment in the export archive.
/// </summary>
/// <param name="SourceContainer">Container holding <paramref name="SourceBlob"/>
/// (e.g. <c>documents-content</c>). Bound by the HMAC capability so the assembler
/// can validate provenance.</param>
/// <param name="SourceBlob">Reference to the source blob owned by the subject.</param>
/// <param name="EntryPath">Path the source will occupy in the archive (e.g.
/// <c>"Documents/2024/invoice.pdf"</c>). Sanitised on entry.</param>
/// <param name="ContentType">MIME type of the source — drives compression mode at
/// archive assembly.</param>
/// <param name="KnownSizeBytes">Best-effort size hint when the provider knows the
/// blob's byte length up front. Used by the sharding writer to plan shard
/// rollovers.</param>
public sealed record BlobBackedExportItem(
    string SourceContainer,
    BlobReference SourceBlob,
    string EntryPath,
    string ContentType,
    long? KnownSizeBytes = null);
