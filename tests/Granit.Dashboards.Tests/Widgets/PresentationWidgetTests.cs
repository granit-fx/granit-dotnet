using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Tests.Widgets;

/// <summary>
/// Locks the public shape of the three presentation-only widgets shipped by
/// <c>Granit.Dashboards.Abstractions</c>. Permission filtering does not apply to
/// these widgets — they are always visible to anyone who can see the dashboard.
/// </summary>
public sealed class PresentationWidgetTests
{
    [Fact]
    public void Markdown_DefaultsToFullWidthRow_AndIgnoresPermissions()
    {
        MarkdownWidgetDefinition widget = new(
            Slug: "Banner",
            ContentLocalizationKey: "Widget:Sample.Banner",
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.FullWidthRow);
        widget.RequiredPermission.ShouldBeNull();
    }

    [Fact]
    public void Image_DefaultsToMediaTile_AndIgnoresPermissions()
    {
        ImageWidgetDefinition widget = new(
            Slug: "Logo",
            Source: "https://cdn.example.com/logo.png",
            AltLocalizationKey: "Widget:Sample.Logo.Alt",
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.MediaTile);
        widget.RequiredPermission.ShouldBeNull();
    }

    [Fact]
    public void Text_DefaultsToFullWidthRow_AndCarriesStyle()
    {
        TextWidgetDefinition widget = new(
            Slug: "Title",
            ContentLocalizationKey: "Widget:Sample.Title",
            Style: TextStyle.Heading,
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.FullWidthRow);
        widget.Style.ShouldBe(TextStyle.Heading);
    }
}
