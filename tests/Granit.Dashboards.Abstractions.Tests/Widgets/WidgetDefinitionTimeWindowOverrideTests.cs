using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests.Widgets;

/// <summary>
/// Locks the propagation of <see cref="WidgetDefinition.TimeWindowOverride"/> through
/// the presentation widget hierarchy. Markdown / Image / Text don't expose a
/// <c>TimeWindowOverride</c> parameter on their primary constructors — they're not
/// data-bound, so the override is meaningless for them — but the base record still
/// carries the field as <c>null</c> so the JSON contract stays uniform.
/// </summary>
public sealed class WidgetDefinitionTimeWindowOverrideTests
{
    [Fact]
    public void PresentationWidgets_AlwaysHaveNullTimeWindowOverride()
    {
        WidgetDefinition[] widgets =
        [
            new MarkdownWidgetDefinition("Banner", "Widget:S.Banner", Position: 0),
            new ImageWidgetDefinition("Logo", "https://x", "Widget:S.Logo.Alt", Position: 1),
            new TextWidgetDefinition("Title", "Widget:S.Title", TextStyle.Heading, Position: 2),
        ];

        foreach (WidgetDefinition widget in widgets)
        {
            widget.TimeWindowOverride.ShouldBeNull();
        }
    }
}
