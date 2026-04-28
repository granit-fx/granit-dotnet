using Granit.Dashboards;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Locks the projection logic backing <c>GET /catalog</c>: descriptor → wire DTO,
/// optional category filter, multi-view widget-count rule, feature-flag fields.
/// Pure in-memory — no WebApplication harness, no auth.
/// </summary>
public sealed class DashboardCatalogProjectionTests
{
    [Fact]
    public void Project_ReturnsAllDescriptors_WhenCategoryNull()
    {
        IDashboardDefinitionDescriptor[] sources =
        [
            new TopFinanceDashboard(),
            new SmallOpsDashboard(),
        ];

        IReadOnlyList<DashboardCatalogEntryResponse> result =
            DashboardCatalogProjection.Project(sources, category: null);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Project_FiltersByCategory()
    {
        IDashboardDefinitionDescriptor[] sources =
        [
            new TopFinanceDashboard(),
            new SmallOpsDashboard(),
        ];

        IReadOnlyList<DashboardCatalogEntryResponse> result =
            DashboardCatalogProjection.Project(sources, DashboardCategory.Finance);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Sample.Finance.Top");
    }

    [Fact]
    public void Project_SingleViewDashboard_ReportsWidgetsCount()
    {
        DashboardCatalogEntryResponse entry =
            DashboardCatalogProjection.ToResponse(new TopFinanceDashboard());

        entry.WidgetCount.ShouldBe(1);
        entry.HasViews.ShouldBeFalse();
        entry.HasAliases.ShouldBeFalse();
        entry.HasFilters.ShouldBeFalse();
    }

    [Fact]
    public void Project_MultiViewDashboard_ReportsEntryViewWidgetCount()
    {
        DashboardCatalogEntryResponse entry =
            DashboardCatalogProjection.ToResponse(new MultiViewDeviceDashboard());

        entry.HasViews.ShouldBeTrue();
        // Default view "list" has 2 widgets; "detail" has 1. Projection picks "list".
        entry.WidgetCount.ShouldBe(2);
    }

    [Fact]
    public void Project_MultiViewDashboard_NullDefaultView_FallsBackToFirstView()
    {
        DashboardCatalogEntryResponse entry =
            DashboardCatalogProjection.ToResponse(new MultiViewWithoutDefaultDashboard());

        entry.HasViews.ShouldBeTrue();
        // No DefaultView → first view ("list") wins, with 1 widget.
        entry.WidgetCount.ShouldBe(1);
    }

    [Fact]
    public void Project_SurfacesFeatureFlags()
    {
        DashboardCatalogEntryResponse entry =
            DashboardCatalogProjection.ToResponse(new RichDashboard());

        entry.HasViews.ShouldBeTrue();
        entry.HasAliases.ShouldBeTrue();
        entry.HasFilters.ShouldBeTrue();
        entry.IsSystem.ShouldBeTrue();
        entry.Version.ShouldBe("2.0.0");
    }

    private sealed class TopFinanceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Finance.Top";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
        [
            new MarkdownWidgetDefinition("Banner", "Widget:Banner", Position: 0),
        ];
    }

    private sealed class SmallOpsDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Ops.Small";
        public override DashboardCategory Category => DashboardCategory.Operations;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
    }

    private sealed class MultiViewDeviceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Devices.MultiView";
        public override DashboardCategory Category => DashboardCategory.Iot;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
        public override string? DefaultView => "list";
        public override IReadOnlyList<DashboardView>? Views { get; } =
        [
            new DashboardView("list",
            [
                new TextWidgetDefinition("Title", "Widget:Title", TextStyle.Heading, Position: 0),
                new MarkdownWidgetDefinition("Description", "Widget:Description", Position: 1),
            ]),
            new DashboardView("detail",
            [
                new MarkdownWidgetDefinition("DetailBody", "Widget:DetailBody", Position: 0),
            ]),
        ];
    }

    private sealed class MultiViewWithoutDefaultDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Devices.NoDefault";
        public override DashboardCategory Category => DashboardCategory.Iot;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
        public override IReadOnlyList<DashboardView>? Views { get; } =
        [
            new DashboardView("list",
            [
                new MarkdownWidgetDefinition("OnlyOne", "Widget:OnlyOne", Position: 0),
            ]),
            new DashboardView("detail",
            [
                new MarkdownWidgetDefinition("Body", "Widget:Body", Position: 0),
                new TextWidgetDefinition("T", "Widget:T", TextStyle.Body, Position: 1),
            ]),
        ];
    }

    private sealed class RichDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Rich";
        public override DashboardCategory Category => DashboardCategory.Compliance;
        public override bool IsSystem => true;
        public override string Version => "2.0.0";
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
        public override IReadOnlyList<DashboardView>? Views { get; } =
        [
            new DashboardView("default",
            [
                new MarkdownWidgetDefinition("X", "Widget:X", Position: 0),
            ]),
        ];
        public override IReadOnlyList<EntityAlias>? Aliases { get; } =
        [
            new EntityAlias("currentTenant", "Tenant", new TenantContextResolver()),
        ];
        public override IReadOnlyList<DashboardFilter>? Filters { get; } =
        [
            new DashboardFilter(
                "Severity",
                "Dashboard:Sample.Rich.Filter.Severity",
                [new DashboardFilterClause("severity", DashboardFilterOperator.Gte, "warning")]),
        ];
    }
}
