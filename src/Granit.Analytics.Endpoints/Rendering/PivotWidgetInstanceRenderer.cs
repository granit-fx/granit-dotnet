using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;
using Granit.Timing;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Pivot"</c> widget kind.
/// Resolves the registered <see cref="IPivotRunner"/> by query name, runs it
/// with the configured row / column dimensions plus the value aggregation,
/// and shapes the result into a <see cref="PivotWidgetSnapshot"/>. Per-widget
/// permission gate + error isolation already apply upstream
/// (<see cref="IDashboardRenderer"/> / ADR-039 §3); this renderer is
/// responsible for the body shape only.
/// </summary>
internal sealed class PivotWidgetInstanceRenderer(
    PivotService pivotService,
    IClock clock) : IWidgetInstanceRenderer
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PivotService _pivotService = pivotService;
    private readonly IClock _clock = clock;

    public string WidgetType => "Pivot";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        PivotConfig config = JsonSerializer.Deserialize<PivotConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Pivot') has empty ConfigJson — pivot config cannot be resolved.");

        if (string.IsNullOrWhiteSpace(widget.QueryName))
        {
            throw new InvalidOperationException(
                $"Widget {widget.Id} ('Pivot') has no QueryName — Pivot widgets must reference a registered QueryDefinition.");
        }

        DateTimeOffset emittedAt = _clock.Now;

        if (!_pivotService.TryGetRunner(widget.QueryName, out IPivotRunner runner))
        {
            return WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: RefreshHint.Static,
                reasonLocalizationKey: "Widget:Unavailable.QueryNotFound");
        }

        IReadOnlyList<string> rowFields = config.RowFields ?? [];
        IReadOnlyList<string> columnFields = config.ColumnFields ?? [];

        PivotRunnerResult result = await runner.ExecuteAsync(
            rowFields: rowFields,
            columnFields: columnFields,
            valueField: config.ValueField,
            aggregation: config.ValueAggregation,
            dashboardFilters: context.DashboardFilters,
            cancellationToken).ConfigureAwait(false);

        PivotWidgetSnapshot snapshot = new(
            RowFields: rowFields,
            ColumnFields: columnFields,
            ValueField: config.ValueField,
            Aggregation: config.ValueAggregation,
            Cells: [.. result.Cells.Select(c => new PivotCell(c.RowKeys, c.ColumnKeys, c.Value))],
            Currency: result.CurrencyCode);

        return WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: emittedAt,
            refreshHint: RefreshHint.Dynamic);
    }

    private sealed record PivotConfig(
        IReadOnlyList<string>? RowFields,
        IReadOnlyList<string>? ColumnFields,
        string? ValueField,
        AggregateFunction ValueAggregation);
}
