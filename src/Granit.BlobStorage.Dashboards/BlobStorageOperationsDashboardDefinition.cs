using Granit.Analytics.Dashboards.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Widgets;
using Granit.QueryEngine.Filtering;

namespace Granit.BlobStorage.Dashboards;

/// <summary>
/// Storage operations dashboard — valid / orphan blob counts, total storage
/// footprint, plus a daily upload chart. First-wave reference for the
/// <see cref="DashboardDefinition"/> pattern in the BlobStorage module.
/// </summary>
public sealed class BlobStorageOperationsDashboardDefinition : DashboardDefinition
{
    /// <inheritdoc />
    public override string Name => "Granit.BlobStorage.StorageOperations";

    /// <inheritdoc />
    public override DashboardCategory Category => DashboardCategory.Operations;

    /// <inheritdoc />
    public override IReadOnlyList<WidgetDefinition> Widgets { get; } =
    [
        new MarkdownWidgetDefinition(
            Slug: "banner",
            ContentLocalizationKey: "Widget:Granit.BlobStorage.StorageOperations.banner.Body",
            Position: 0),

        new KpiWidgetDefinition(
            Slug: "valid-count",
            Datasource: Datasource.Metric("Granit.BlobStorage.ValidBlobDescriptorCountMetric"),
            Position: 1),

        new KpiWidgetDefinition(
            Slug: "storage-total",
            Datasource: Datasource.Metric("Granit.BlobStorage.ValidBlobDescriptorSizeTotalMetric"),
            Position: 2),

        new KpiWidgetDefinition(
            Slug: "orphan-count",
            Datasource: Datasource.Metric("Granit.BlobStorage.OrphanBlobDescriptorCountMetric"),
            Position: 3),

        new ChartWidgetDefinition(
            Slug: "uploads-by-day",
            QueryName: "Granit.BlobStorage.BlobDescriptorsQuery",
            GroupBy: "CreatedAt",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Bar,
            Position: 4),
    ];
}
