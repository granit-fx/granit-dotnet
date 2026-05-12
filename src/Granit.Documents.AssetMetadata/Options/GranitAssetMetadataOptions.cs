using System;
using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.AssetMetadata.Options;

/// <summary>Configuration options for <c>Granit.Documents.AssetMetadata</c>.</summary>
public sealed class GranitAssetMetadataOptions
{
    /// <summary>Configuration section key (<c>"Documents:AssetMetadata"</c>).</summary>
    public const string SectionName = "Documents:AssetMetadata";

    /// <summary>
    /// Master GPS-scrub toggle for image uploads. When <c>true</c> (the default —
    /// GDPR friendliness):
    /// <list type="bullet">
    ///   <item>
    ///     The F17.9 <c>StripGpsHandler</c> (in <c>Granit.Documents.AssetMetadata.Imaging</c>)
    ///     synchronously re-uploads a scrubbed copy of the original blob, swaps the
    ///     version's <c>BlobDescriptorId</c> in place, and soft-deletes the original —
    ///     GPS coordinates never reach cold storage.
    ///   </item>
    ///   <item>
    ///     The F17.5 <c>ImageMetadataExtractor</c> drops GPS columns from the typed
    ///     <c>AssetMetadataResult</c> projection and from the raw archive — defence
    ///     in depth in case the scrub handler missed the upload (older blobs,
    ///     non-JPEG formats not yet covered by the scrubber).
    ///   </item>
    /// </list>
    /// Hosts that legitimately need to keep GPS (real-estate, journalism, geo-tagging)
    /// flip the flag off; both flows then short-circuit and the original bytes are
    /// preserved verbatim.
    /// </summary>
    public bool StripGpsOnUpload { get; set; } = true;

    /// <summary>
    /// When <c>true</c> (the default — GDPR Art. 5(c) data-minimisation), drops
    /// PII-bearing keys from <c>RawMetadata</c> and nulls the matching typed
    /// columns (<see cref="Domain.DocumentAssetMetadata.Author"/>,
    /// <see cref="Domain.DocumentAssetMetadata.Artist"/>,
    /// <see cref="Domain.DocumentAssetMetadata.LastModifiedBy"/>) after every
    /// extractor run. The substring allow-list (<c>author</c>, <c>owner</c>,
    /// <c>artist</c>, <c>copyright</c>, <c>creator</c>, <c>contact</c>,
    /// <c>byline</c>, <c>credit</c>, <c>serial</c>, <c>cameraownername</c>,
    /// <c>lastmodifiedby</c>) is curated in <c>PersonalDataKeyMatcher</c>.
    /// Hosts that legitimately need attribution (DAM, photo-journalism, asset
    /// licensing) flip the flag off and accept the residual PII risk.
    /// </summary>
    public bool StripPersonalDataOnUpload { get; set; } = true;

    /// <summary>Cap on simultaneous background extractions per host.</summary>
    [Range(1, 64)]
    public int MaxConcurrentExtractions { get; set; } = 4;

    /// <summary>
    /// Best-effort hard timeout for a single extractor invocation.
    /// The underlying NuGet extractors (MetadataExtractor, PdfPig, OpenXml,
    /// TagLibSharp) are synchronous; the timed-out task continues running in a
    /// thread-pool thread until the synchronous core returns, but the
    /// orchestration unblocks via a linked <c>CancellationTokenSource</c> and
    /// the next document can be processed. A timeout is reported via
    /// <c>AssetMetadataMetrics.RecordTimeout(...)</c>.
    /// </summary>
    public TimeSpan ExtractionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
