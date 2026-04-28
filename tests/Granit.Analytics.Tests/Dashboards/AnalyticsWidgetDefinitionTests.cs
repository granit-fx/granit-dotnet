using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

/// <summary>
/// Smoke-level coverage of the four analytics widget definition shapes shipped by
/// <c>Granit.Analytics</c>. The purpose is to lock the public API (default sizes,
/// nullable selectors, optional permission overrides), not to re-test record equality.
/// </summary>
public sealed class AnalyticsWidgetDefinitionTests
{
    [Fact]
    public void Kpi_DefaultsToSmallKpiSize()
    {
        KpiWidgetDefinition widget = new(
            "UnpaidCount", Datasource.Metric("Sample.UnpaidInvoiceCountMetric"), Position: 0);

        widget.Size.ShouldBe(WidgetSize.SmallKpi);
        widget.RequiredPermission.ShouldBeNull();
        widget.Datasource.ShouldBeOfType<MetricDatasource>();
    }

    [Fact]
    public void Kpi_AllowsRequiredPermissionOverride()
    {
        KpiWidgetDefinition widget = new(
            Slug: "RestrictedKpi",
            Datasource: Datasource.Metric("Sample.Metric"),
            Position: 0,
            RequiredPermission: "Custom.Composite.Read");

        widget.RequiredPermission.ShouldBe("Custom.Composite.Read");
    }

    [Fact]
    public void Chart_DefaultsToStandardChartSize()
    {
        ChartWidgetDefinition widget = new(
            Slug: "RevenueByMonth",
            QueryName: "Sample.InvoiceQuery",
            GroupBy: "IssuedAtMonth",
            Aggregation: AggregateFunction.Sum,
            Field: "Total",
            ChartType: ChartType.Line,
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.StandardChart);
        widget.ChartType.ShouldBe(ChartType.Line);
    }

    [Fact]
    public void Table_AllowsNullVisibleColumns_AndCustomPageSize()
    {
        TableWidgetDefinition widget = new(
            Slug: "RecentInvoices",
            QueryName: "Sample.InvoiceQuery",
            VisibleColumns: null,
            PageSize: 25,
            Position: 0);

        widget.VisibleColumns.ShouldBeNull();
        widget.PageSize.ShouldBe(25);
    }

    [Fact]
    public void Pivot_AllowsNullValueField_WhenAggregationIsCount()
    {
        PivotWidgetDefinition widget = new(
            Slug: "InvoicesByCustomerByMonth",
            QueryName: "Sample.InvoiceQuery",
            RowFields: ["CustomerName"],
            ColumnFields: ["IssuedAtMonth"],
            ValueField: null,
            ValueAggregation: AggregateFunction.Count,
            Position: 0);

        widget.ValueField.ShouldBeNull();
        widget.ValueAggregation.ShouldBe(AggregateFunction.Count);
    }

    [Fact]
    public void Map_DefaultsToMediaTileSize_AndCarriesLatLngSource()
    {
        MapWidgetDefinition widget = new(
            Slug: "Customers",
            QueryName: "Sample.PartyQuery",
            PointSource: new MapPointSource.LatLng("Latitude", "Longitude"),
            PopupColumns: ["Name", "City"],
            Position: 0);

        widget.Size.ShouldBe(WidgetSize.MediaTile);
        widget.DefaultZoom.ShouldBe(5);
        widget.DefaultCenter.ShouldBeNull();
        widget.ClusterThreshold.ShouldBe(200);
        widget.DetailRoute.ShouldBeNull();
        widget.TileUrlTemplate.ShouldBeNull();
        MapPointSource.LatLng latLng = widget.PointSource.ShouldBeOfType<MapPointSource.LatLng>();
        latLng.LatitudeColumn.ShouldBe("Latitude");
        latLng.LongitudeColumn.ShouldBe("Longitude");
    }

    [Fact]
    public void Map_PostGisVariant_ExposesGeographyColumn()
    {
        MapWidgetDefinition widget = new(
            Slug: "Offices",
            QueryName: "Sample.OfficeQuery",
            PointSource: new MapPointSource.Geography("Location"),
            PopupColumns: null,
            Position: 0);

        MapPointSource.Geography geo = widget.PointSource.ShouldBeOfType<MapPointSource.Geography>();
        geo.GeographyColumn.ShouldBe("Location");
        widget.PopupColumns.ShouldBeNull();
    }

    [Fact]
    public void MapCenter_RejectsLatitudeOutsideRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MapCenter(latitude: 91.0, longitude: 0.0));
        Should.Throw<ArgumentOutOfRangeException>(() => new MapCenter(latitude: -90.5, longitude: 0.0));
    }

    [Fact]
    public void MapCenter_RejectsLongitudeOutsideRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MapCenter(latitude: 0.0, longitude: 180.5));
        Should.Throw<ArgumentOutOfRangeException>(() => new MapCenter(latitude: 0.0, longitude: -181.0));
    }

    [Fact]
    public void MapCenter_AcceptsBoundaryCoordinates()
    {
        Should.NotThrow(() => new MapCenter(90.0, 180.0));
        Should.NotThrow(() => new MapCenter(-90.0, -180.0));
        Should.NotThrow(() => new MapCenter(0.0, 0.0));
    }
}
