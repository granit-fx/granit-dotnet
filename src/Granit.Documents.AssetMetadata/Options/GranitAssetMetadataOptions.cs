using System;
using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.AssetMetadata.Options;

/// <summary>Configuration options for <c>Granit.Documents.AssetMetadata</c>.</summary>
public sealed class GranitAssetMetadataOptions
{
    /// <summary>Configuration section key (<c>"Documents:AssetMetadata"</c>).</summary>
    public const string SectionName = "Documents:AssetMetadata";

    /// <summary>
    /// Strip GPS coordinates (and camera serial numbers when surfaced by the
    /// extractor) from image uploads <i>before</i> the bytes land in their final
    /// blob. Default <c>true</c> — RGPD friendliness; hosts that legitimately
    /// need to keep GPS (real-estate, journalism, geo-tagging) flip the flag off.
    /// </summary>
    public bool StripGpsOnUpload { get; set; } = true;

    /// <summary>Cap on simultaneous background extractions per host.</summary>
    [Range(1, 64)]
    public int MaxConcurrentExtractions { get; set; } = 4;

    /// <summary>Hard timeout for a single extractor invocation.</summary>
    public TimeSpan ExtractionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
