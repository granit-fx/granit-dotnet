using System.ComponentModel.DataAnnotations;

namespace Granit.TextExtraction.Options;

/// <summary>
/// Configuration options for <c>Granit.TextExtraction</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The defaults are tuned for typical enterprise document ingestion (≤ 100 MB uploads,
/// ≤ 500 k characters indexed per document — the Postgres tsvector hard limit is ~1 MB).
/// Hosts should tighten them for high-throughput pipelines or relax them for trusted
/// internal corpora.
/// </remarks>
public sealed class GranitTextExtractionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction";

    /// <summary>
    /// Maximum number of extractions allowed to run concurrently across the host process.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxConcurrentExtractions { get; set; } = Environment.ProcessorCount;

    /// <summary>Per-extraction timeout. Defaults to 30 seconds.</summary>
    public TimeSpan ExtractionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Hard cap on the compressed input stream — enforced by <see cref="Granit.TextExtraction.LimitedStream"/>.
    /// Defaults to 100 MB. Breaching it raises
    /// <see cref="Granit.TextExtraction.Exceptions.TextExtractionException"/> with reason <c>input_too_large</c>.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxBodySizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// Zip-bomb ceiling for archive-shaped extractors (Office, ZIP). Defaults to 500 MB.
    /// Enforced by extractors that walk decompressed entries.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxDecompressedBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>
    /// Maximum number of entries an archive (e.g. an Office .docx) is allowed to contain.
    /// Defaults to 10 000. Protects against archive-entry DoS.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxZipEntries { get; set; } = 10_000;

    /// <summary>
    /// Maximum number of characters returned by any extractor. Defaults to 500 000
    /// (≈ 1 MB UTF-8 — Postgres tsvector hard limit and a safe LOH bomb ceiling).
    /// Producing more characters MUST result in
    /// <see cref="Granit.TextExtraction.TextExtractionResult.IsTruncated"/> set to <c>true</c>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxExtractedCharLength { get; set; } = 500_000;

    /// <summary>
    /// Maximum total pixels for image rasterisation (PDF page rendering, OCR pre-processing).
    /// Defaults to 100 000 000 (≈ 10 000 × 10 000). Guard against pixel-flood attacks.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxImagePixels { get; set; } = 100_000_000;
}
