using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Table"</c> widget kind.
/// Resolves the registered <see cref="ITableRunner"/> by query name, runs it
/// against the entity's queryable through the QueryEngine pipeline, and
/// shapes the result into a <see cref="TableWidgetSnapshot"/>. Per-widget
/// permission gate + error isolation already apply upstream
/// (<see cref="IDashboardRenderer"/> / ADR-039 §3); this renderer is
/// responsible for the body shape only.
/// </summary>
internal sealed class TableWidgetInstanceRenderer(
    TableService tableService,
    IClock clock) : IWidgetInstanceRenderer
{
    // ConfigJson was written by WidgetDefinitionToInstanceMapper with
    // PropertyNamingPolicy = CamelCase + (no enum converter). Match.
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly TableService _tableService = tableService;
    private readonly IClock _clock = clock;

    public string WidgetType => "Table";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        TableConfig config = JsonSerializer.Deserialize<TableConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Table') has empty ConfigJson — table config cannot be resolved.");

        if (string.IsNullOrWhiteSpace(widget.QueryName))
        {
            throw new InvalidOperationException(
                $"Widget {widget.Id} ('Table') has no QueryName — Table widgets must reference a registered QueryDefinition.");
        }

        DateTimeOffset emittedAt = _clock.Now;

        if (!_tableService.TryGetRunner(widget.QueryName, out ITableRunner runner))
        {
            return WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: RefreshHint.Static,
                reasonLocalizationKey: "Widget:Unavailable.QueryNotFound");
        }

        int pageSize = config.PageSize > 0
            ? config.PageSize
            : DefaultPageSize;

        TableRunnerResult result = await runner
            .ExecuteAsync(config.VisibleColumns, pageSize, context.DashboardFilters, cancellationToken)
            .ConfigureAwait(false);

        TableWidgetSnapshot snapshot = new(
            Columns: [.. result.Columns.Select(c => new TableWidgetColumn(c.Name, c.LabelLocalizationKey, c.CurrencyCode))],
            Rows: result.Rows,
            TotalRowCount: result.TotalRowCount);

        return WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: emittedAt,
            refreshHint: RefreshHint.Dynamic);
    }

    /// <summary>
    /// Default rows-per-tile when the widget config omits <c>pageSize</c> —
    /// 10 rows fits a typical KPI-row layout without scrolling.
    /// </summary>
    private const int DefaultPageSize = 10;

    private sealed record TableConfig(IReadOnlyList<string>? VisibleColumns, int PageSize);
}
