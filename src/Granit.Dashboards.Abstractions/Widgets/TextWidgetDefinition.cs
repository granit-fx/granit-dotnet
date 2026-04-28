namespace Granit.Dashboards.Widgets;

/// <summary>
/// Plain text tile — short label or heading without markdown formatting. Lighter
/// alternative to <see cref="MarkdownWidgetDefinition"/> for stable interface labels
/// (page titles, section headers) where markdown rendering would be overkill.
/// </summary>
/// <param name="Slug">See <see cref="WidgetDefinition.Slug"/>.</param>
/// <param name="ContentLocalizationKey">Localization key for the text body.</param>
/// <param name="Style">Visual style hint — interpreted by the frontend.</param>
/// <param name="Position">See <see cref="WidgetDefinition.Position"/>.</param>
/// <param name="Size">Defaults to <see cref="WidgetSize.FullWidthRow"/>.</param>
/// <param name="Actions">See <see cref="WidgetDefinition.Actions"/>.</param>
public sealed record TextWidgetDefinition(
    string Slug,
    string ContentLocalizationKey,
    TextStyle Style,
    int Position,
    WidgetSize? Size = null,
    IReadOnlyList<WidgetAction>? Actions = null)
    : WidgetDefinition(Slug, Position, Size ?? WidgetSize.FullWidthRow, RequiredPermission: null, TimeWindowOverride: null, Actions);

/// <summary>Visual style hint for <see cref="TextWidgetDefinition"/>.</summary>
public enum TextStyle
{
    /// <summary>Default body-text size and weight.</summary>
    Body = 0,

    /// <summary>Page-level heading (h1).</summary>
    Heading = 1,

    /// <summary>Section subheading (h2 / h3).</summary>
    Subheading = 2,

    /// <summary>Smaller, secondary caption text.</summary>
    Caption = 3,
}
