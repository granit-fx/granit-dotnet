namespace Granit.Dashboards.Rendering;

/// <summary>
/// Wire-shape snapshot for the <c>"Markdown"</c> widget kind. Carries the
/// localization key the frontend resolves to a markdown body — the renderer
/// itself never resolves the key (the active locale lives on the client and
/// the localization bundle ships separately), so the snapshot stays static
/// regardless of the requesting user's culture.
/// </summary>
/// <param name="ContentLocalizationKey">Localization key whose value is the markdown body (e.g. <c>"Widget:Granit.Invoicing.FinanceOverview.Banner"</c>).</param>
public sealed record MarkdownWidgetSnapshot(string ContentLocalizationKey);
