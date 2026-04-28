using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests.Widgets;

/// <summary>
/// Confirms the <see cref="WidgetDefinition.Actions"/> field propagates through the
/// three presentation widgets. Default = <c>null</c> (no actions). Explicit value
/// is preserved by record equality and round-trips through the base.
/// </summary>
public sealed class WidgetDefinitionActionsPropagationTests
{
    [Fact]
    public void Markdown_DefaultsToNoActions()
        => new MarkdownWidgetDefinition("M", "Widget:M", Position: 0).Actions.ShouldBeNull();

    [Fact]
    public void Image_DefaultsToNoActions()
        => new ImageWidgetDefinition("I", "https://x", "Widget:I.Alt", Position: 0).Actions.ShouldBeNull();

    [Fact]
    public void Text_DefaultsToNoActions()
        => new TextWidgetDefinition("T", "Widget:T", TextStyle.Body, Position: 0).Actions.ShouldBeNull();

    [Fact]
    public void Markdown_ExposesActionsForBannerLinks()
    {
        WidgetAction[] actions =
        [
            new(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/docs/onboarding"),
        ];

        MarkdownWidgetDefinition widget = new(
            "Banner", "Widget:Banner", Position: 0, Actions: actions);

        widget.Actions.ShouldNotBeNull();
        widget.Actions.Count.ShouldBe(1);
        widget.Actions[0].Kind.ShouldBe(WidgetActionKind.Navigate);
    }

    [Fact]
    public void Image_ExposesActions_ForLogoLinks()
    {
        WidgetAction[] actions =
        [
            new(WidgetActionTrigger.Click, WidgetActionKind.OpenDashboard, "Granit.Invoicing.FinanceOverview"),
        ];

        ImageWidgetDefinition widget = new(
            "Logo", "blob:logo", "Widget:Logo.Alt", Position: 0, Actions: actions);

        widget.Actions.ShouldNotBeNull();
        widget.Actions[0].Target.ShouldBe("Granit.Invoicing.FinanceOverview");
    }
}
