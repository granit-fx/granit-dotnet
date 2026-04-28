using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Confirms that <see cref="DashboardDefinition.Views"/> + <c>DefaultView</c>
/// surface correctly through the descriptor and default to <c>null</c>
/// (single-view backward compat).
/// </summary>
public sealed class DashboardDefinitionViewsTests
{
    [Fact]
    public void SingleView_HasNoViewsAndNoDefault()
    {
        IDashboardDefinitionDescriptor d = new SingleViewDashboard();

        d.Views.ShouldBeNull();
        d.DefaultView.ShouldBeNull();
        d.Widgets.Count.ShouldBe(1);
    }

    [Fact]
    public void MultiView_SurfacesViewsThroughDescriptor()
    {
        IDashboardDefinitionDescriptor d = new MultiViewDashboard();

        d.Views.ShouldNotBeNull();
        d.Views.Count.ShouldBe(2);
        d.Views.ShouldContain(v => v.Name == "list");
        d.Views.ShouldContain(v => v.Name == "detail");
        d.DefaultView.ShouldBe("list");
    }

    [Fact]
    public void MultiView_LeavesWidgetsEmpty_ByConvention()
    {
        // Multi-view dashboards delegate widget lists to each named view; the
        // top-level Widgets property stays empty by convention.
        IDashboardDefinitionDescriptor d = new MultiViewDashboard();

        d.Widgets.ShouldBeEmpty();
    }

    private sealed class SingleViewDashboard : DashboardDefinition
    {
        public override string Name => "Sample.SingleView";
        public override DashboardCategory Category => DashboardCategory.General;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
        [
            new MarkdownWidgetDefinition("Banner", "Widget:Sample.SingleView.Banner", Position: 0),
        ];
    }

    private sealed class MultiViewDashboard : DashboardDefinition
    {
        public override string Name => "Sample.MultiView";
        public override DashboardCategory Category => DashboardCategory.Operations;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
        public override string? DefaultView => "list";

        public override IReadOnlyList<DashboardView>? Views { get; } =
        [
            new DashboardView(
                "list",
                [new MarkdownWidgetDefinition("Header", "Widget:Sample.MultiView.list.Header", Position: 0)]),
            new DashboardView(
                "detail",
                [new MarkdownWidgetDefinition("Body", "Widget:Sample.MultiView.detail.Body", Position: 0)]),
        ];
    }
}
