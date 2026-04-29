using Granit.Dashboards.Widgets;

namespace Granit.Dashboards.Endpoints.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Text"</c> widget kind — a plain-text tile
/// (page heading, section subheading, caption) with no data binding. Mirrors
/// the declarative <see cref="TextWidgetDefinition"/> 1-to-1; the frontend
/// resolves the localization key against the active culture.
/// </summary>
/// <param name="ContentLocalizationKey">Localization key for the text body.</param>
/// <param name="Style">Visual style hint inherited from <see cref="TextWidgetDefinition.Style"/>.</param>
public sealed record TextWidgetSnapshot(
    string ContentLocalizationKey,
    TextStyle Style);
