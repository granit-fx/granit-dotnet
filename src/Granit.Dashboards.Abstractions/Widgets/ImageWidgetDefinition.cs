namespace Granit.Dashboards.Widgets;

/// <summary>
/// Static image tile — typically a logo, illustration, or contextual photo. The
/// source can be an inline URL (CDN, public asset) or a Granit blob reference
/// resolved at render time by the frontend.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="Source">URL or blob reference (e.g. <c>"blob:logo-banner"</c>, <c>"https://cdn..."</c>).</param>
/// <param name="AltLocalizationKey">Localization key for the alt text (accessibility).</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.MediaTile"/>.</param>
/// <param name="Fit">
/// How the image fills the cell. Defaults to <see cref="ImageFit.Contain"/>
/// (preserves aspect, letterboxes) — the right call for logos. Use
/// <see cref="ImageFit.Cover"/> for banner photos that should fill the tile.
/// </param>
public sealed record ImageWidgetDefinition(
    string Slug,
    string Source,
    string AltLocalizationKey,
    int Position,
    WidgetSize? Size = null,
    ImageFit Fit = ImageFit.Contain)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.MediaTile, RequiredPermission: null);

/// <summary>How an <see cref="ImageWidgetDefinition"/> fills its grid cell.</summary>
public enum ImageFit
{
    /// <summary>Preserve aspect ratio, letterbox to fit. Right for logos.</summary>
    Contain = 0,

    /// <summary>Fill the cell, crop to maintain aspect. Right for banner photos.</summary>
    Cover = 1,

    /// <summary>Stretch to fill (rarely correct — distorts the image).</summary>
    Fill = 2,
}

