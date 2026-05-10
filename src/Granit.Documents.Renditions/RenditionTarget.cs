using Granit.Documents.Renditions.Domain;

namespace Granit.Documents.Renditions;

/// <summary>
/// What the caller wants the pipeline to produce. Combines the kind of rendition
/// (<see cref="Type"/>), the desired output MIME type (<see cref="TargetContentType"/>),
/// and an optional dimension hint that providers honour as a best effort.
/// </summary>
/// <param name="Type">Kind of rendition driving the policy decisions (e.g. quality, dimension defaults).</param>
/// <param name="TargetContentType">Output MIME type (e.g. <c>"image/webp"</c>, <c>"image/png"</c>).</param>
/// <param name="Dimensions">Optional dimensions hint; providers may downscale source content but not upscale.</param>
/// <param name="Quality">Optional quality knob in [0, 100] for lossy outputs.</param>
public sealed record RenditionTarget(
    RenditionType Type,
    string TargetContentType,
    RenditionDimensions? Dimensions = null,
    int? Quality = null);

/// <summary>Pixel dimensions hint for a rendition.</summary>
/// <param name="Width">Width in pixels — must be strictly positive.</param>
/// <param name="Height">Height in pixels — must be strictly positive.</param>
public sealed record RenditionDimensions(int Width, int Height);
