using System;
using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.Renditions.Options;

/// <summary>Configuration options for <c>Granit.Documents.Renditions</c>.</summary>
public sealed class GranitRenditionsOptions
{
    /// <summary>Configuration section key (<c>"Documents:Renditions"</c>).</summary>
    public const string SectionName = "Documents:Renditions";

    /// <summary>
    /// Maximum number of provider hops the pipeline solver will assemble. Default 3 —
    /// enough for the planned <c>office → pdf → image</c> chain plus headroom; raise only
    /// when a custom provider graph genuinely requires longer chains.
    /// </summary>
    [Range(1, 8)]
    public int MaxChainLength { get; set; } = 3;

    /// <summary>Default thumbnail dimensions emitted by the inline-thumbnail hook (F16.5).</summary>
    public ThumbnailDefaults Thumbnail { get; set; } = new();

    /// <summary>
    /// Maximum bytes for the inline thumbnail emitted at upload time. Generated thumbnails
    /// exceeding this cap are dropped and rescheduled for the background job.
    /// Default 100 KB.
    /// </summary>
    [Range(1_024, 10 * 1024 * 1024)]
    public long InlineThumbnailMaxBytes { get; set; } = 100 * 1024;

    /// <summary>Cap on simultaneous background generation jobs per host.</summary>
    [Range(1, 64)]
    public int MaxConcurrentGenerations { get; set; } = 4;

    /// <summary>
    /// Time-to-live for a recently-generated rendition before the on-demand fallback
    /// considers regenerating it. Default 24 hours — renditions are stable as long as
    /// the source <c>DocumentVersion</c> doesn't change, so the TTL exists mainly to
    /// detect upstream provider upgrades. <see cref="TimeSpan.Zero"/> disables the
    /// staleness check.
    /// </summary>
    public TimeSpan OnDemandStaleness { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>Default thumbnail policy applied to image uploads.</summary>
public sealed class ThumbnailDefaults
{
    /// <summary>Target width in pixels. Default 200.</summary>
    [Range(16, 4096)]
    public int Width { get; set; } = 200;

    /// <summary>Target height in pixels. Default 200.</summary>
    [Range(16, 4096)]
    public int Height { get; set; } = 200;

    /// <summary>Output MIME type for inline thumbnails. Default <c>image/webp</c>.</summary>
    public string Format { get; set; } = "image/webp";

    /// <summary>Lossy quality knob in [0, 100] for the inline thumbnail. Default 75.</summary>
    [Range(0, 100)]
    public int Quality { get; set; } = 75;
}
