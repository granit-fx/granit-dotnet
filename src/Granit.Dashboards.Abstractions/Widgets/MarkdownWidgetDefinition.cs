namespace Granit.Dashboards.Widgets;

/// <summary>
/// Static markdown content — does not query the data layer. Used for dashboard
/// banners, contextual notes, links to runbooks. Permission filtering does not apply
/// (markdown is always visible to anyone who can see the dashboard).
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="ContentLocalizationKey">Localization key resolving to the markdown body (e.g. <c>"Widget:Granit.Invoicing.FinanceOverview.Banner"</c>).</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.FullWidthRow"/>.</param>
/// <param name="Actions">See <see cref="WidgetDefinition.Actions"/>.</param>
public sealed record MarkdownWidgetDefinition(
    string Slug,
    string ContentLocalizationKey,
    int Position,
    WidgetSize? Size = null,
    IReadOnlyList<WidgetAction>? Actions = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.FullWidthRow, RequiredPermission: null, TimeWindowOverride: null, Actions);
