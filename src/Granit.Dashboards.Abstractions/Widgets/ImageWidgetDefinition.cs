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
public sealed record ImageWidgetDefinition(
    string Slug,
    string Source,
    string AltLocalizationKey,
    int Position,
    WidgetSize? Size = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.MediaTile, RequiredPermission: null);
