using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Locks the projection logic backing <c>GET /</c> + <c>GET /{id}</c> — pure
/// in-memory tests, no DbContext required.
/// </summary>
public sealed class DashboardInstanceProjectionTests
{
    [Fact]
    public void ToSummary_StripsWidgetTreeButReportsCount()
    {
        Dashboard dashboard = NewDashboardWithWidgets(2);

        DashboardSummaryResponse summary = DashboardInstanceProjection.ToSummary(dashboard);

        summary.Id.ShouldBe(dashboard.Id);
        summary.Name.ShouldBe(dashboard.Name);
        summary.WidgetCount.ShouldBe(2);
    }

    [Fact]
    public void ToDetail_OrdersWidgetsByPosition()
    {
        var dashboard = Dashboard.Create(Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        // Inserted out of position-order on purpose.
        dashboard.AddWidget(Guid.NewGuid(), "Markdown", position: 2, width: 12, height: 1, titleLocalizationKey: "Widget:S.Two", configJson: "{}");
        dashboard.AddWidget(Guid.NewGuid(), "Markdown", position: 0, width: 12, height: 1, titleLocalizationKey: "Widget:S.Zero", configJson: "{}");
        dashboard.AddWidget(Guid.NewGuid(), "Markdown", position: 1, width: 12, height: 1, titleLocalizationKey: "Widget:S.One", configJson: "{}");

        DashboardDetailResponse detail = DashboardInstanceProjection.ToDetail(dashboard);

        detail.Widgets.Select(w => w.Position).ShouldBe([0, 1, 2]);
        detail.Widgets[0].TitleLocalizationKey.ShouldBe("Widget:S.Zero");
    }

    [Fact]
    public void ToDetail_CarriesLayoutValues()
    {
        var dashboard = Dashboard.Create(
            Guid.NewGuid(), "Sample", DashboardCategory.Finance,
            layoutColumns: 16, layoutRowHeight: 64);

        DashboardDetailResponse detail = DashboardInstanceProjection.ToDetail(dashboard);

        detail.LayoutColumns.ShouldBe(16);
        detail.LayoutRowHeight.ShouldBe(64);
    }

    [Fact]
    public void ToWidgetInstance_PreservesAllFields()
    {
        var dashboard = Dashboard.Create(Guid.NewGuid(), "S", DashboardCategory.Finance);
        WidgetInstance widget = dashboard.AddWidget(
            Guid.NewGuid(), "Kpi",
            position: 0, width: 3, height: 1,
            titleLocalizationKey: "Widget:S.UnpaidCount",
            configJson: "{\"kind\":\"metric\"}",
            metricName: "Sample.Metric",
            queryName: null,
            requiredPermission: "Custom.Composite.Read");

        WidgetInstanceResponse response = DashboardInstanceProjection.ToWidgetInstance(widget);

        response.WidgetType.ShouldBe("Kpi");
        response.MetricName.ShouldBe("Sample.Metric");
        response.QueryName.ShouldBeNull();
        response.ConfigJson.ShouldContain("metric");
        response.RequiredPermission.ShouldBe("Custom.Composite.Read");
    }

    private static Dashboard NewDashboardWithWidgets(int count)
    {
        var d = Dashboard.Create(Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        for (int i = 0; i < count; i++)
        {
            d.AddWidget(Guid.NewGuid(), "Markdown",
                position: i, width: 12, height: 1,
                titleLocalizationKey: $"Widget:S.{i}",
                configJson: "{}");
        }
        return d;
    }
}
