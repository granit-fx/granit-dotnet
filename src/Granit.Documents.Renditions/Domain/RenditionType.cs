namespace Granit.Documents.Renditions.Domain;

/// <summary>
/// Kind of rendition derived from a <see cref="Granit.Documents.Domain.DocumentVersion"/>.
/// </summary>
/// <remarks>
/// Renditions are projections of the <i>active</i> version, never new versions of their own.
/// A new <c>DocumentVersion</c> invalidates its parent's full rendition set; the F16.4
/// background job regenerates them on the next <c>DocumentVersionAddedEvent</c>.
/// </remarks>
public enum RenditionType
{
    /// <summary>Small preview (typically 150–250 px square) for grid / list views.</summary>
    Thumbnail = 0,

    /// <summary>Web-optimised preview (~1280 px max side, optimised for bandwidth).</summary>
    Web = 1,

    /// <summary>Print-quality export with full resolution and an embedded ICC profile when available.</summary>
    Print = 2,

    /// <summary>Static cover image for a video document (frame extracted from the source).</summary>
    Poster = 3,
}
