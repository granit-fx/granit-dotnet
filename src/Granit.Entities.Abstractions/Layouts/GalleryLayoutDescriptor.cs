namespace Granit.Entities.Layouts;

/// <summary>
/// Gallery layout — image-card grid keyed on a <c>BlobReference</c> property.
/// The renderer (<c>EntityGallery</c>) reads each row's image via the host's
/// blob-storage download endpoint and labels the card with
/// <see cref="TitlePropertyName"/> / <see cref="SubtitlePropertyName"/>.
/// </summary>
/// <remarks>
/// Per ADR-042 §1, only <see cref="ImagePropertyName"/> is mandatory; the
/// title and subtitle fall back to the entity's <c>DisplayProperty</c> /
/// <c>SubtitleProperty</c> when omitted. <see cref="ImagePropertyName"/> MUST
/// resolve to a <c>BlobReference</c> (or <c>BlobReference?</c>) property on the
/// entity — enforced by an architecture test (story #1693). The title /
/// subtitle / image properties must also appear in the matching
/// <c>QueryDefinition</c> column whitelist (story #1694).
/// </remarks>
public sealed record GalleryLayoutDescriptor : EntityListLayoutDescriptor
{
    /// <summary>Property carrying the card image — typed <c>BlobReference</c> (or nullable).</summary>
    public required string ImagePropertyName { get; init; }

    /// <summary>Property used as the card headline. Falls back to the entity's <c>DisplayProperty</c>.</summary>
    public string? TitlePropertyName { get; init; }

    /// <summary>Optional secondary line under the title (e.g. category, tag, status).</summary>
    public string? SubtitlePropertyName { get; init; }

    /// <summary>Card size — controls grid track sizing in the renderer.</summary>
    public GalleryCardSize CardSize { get; init; } = GalleryCardSize.Medium;
}
