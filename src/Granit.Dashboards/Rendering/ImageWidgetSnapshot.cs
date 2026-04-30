using Granit.Dashboards.Widgets;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Image"</c> widget kind — a static image
/// tile (logo, illustration, banner). Mirrors the declarative
/// <see cref="ImageWidgetDefinition"/>; the frontend resolves blob references
/// (<c>"blob:..."</c>) and the alt-text localization key against the active
/// session and culture.
/// </summary>
/// <param name="Source">URL or blob reference (<c>"blob:logo-banner"</c>, <c>"https://cdn..."</c>).</param>
/// <param name="AltLocalizationKey">Localization key for the alt text (accessibility).</param>
/// <param name="Fit">How the image fills its grid cell — see <see cref="ImageFit"/>.</param>
public sealed record ImageWidgetSnapshot(
    string Source,
    string AltLocalizationKey,
    ImageFit Fit);
