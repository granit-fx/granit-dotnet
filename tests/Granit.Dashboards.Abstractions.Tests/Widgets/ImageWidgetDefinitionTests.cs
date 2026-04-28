using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests.Widgets;

public sealed class ImageWidgetDefinitionTests
{
    [Fact]
    public void Fit_DefaultsToContain()
    {
        ImageWidgetDefinition widget = new(
            "Logo", "https://cdn.example/logo.png", "Widget:Sample.Logo.Alt", Position: 0);

        widget.Fit.ShouldBe(ImageFit.Contain);
    }

    [Fact]
    public void Fit_AllowsCoverForBannerPhotos()
    {
        ImageWidgetDefinition widget = new(
            "Banner", "blob:hero", "Widget:Sample.Banner.Alt", Position: 0,
            Fit: ImageFit.Cover);

        widget.Fit.ShouldBe(ImageFit.Cover);
    }

    [Fact]
    public void Fit_EnumOrderingIsStable()
    {
        // Wire format stability — values land in JSON as integers when no
        // converter is set, so the ordering must not drift.
        ((int)ImageFit.Contain).ShouldBe(0);
        ((int)ImageFit.Cover).ShouldBe(1);
        ((int)ImageFit.Fill).ShouldBe(2);
    }
}
