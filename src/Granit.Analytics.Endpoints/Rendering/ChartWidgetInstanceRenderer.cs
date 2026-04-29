using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Dashboards.Widgets;
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
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Chart"</c> widget kind.
/// Resolves the registered <see cref="IChartRunner"/> by query name, runs it
/// with the configured <c>groupBy</c> + <c>aggregation</c> + <c>field</c>,
/// and shapes the result into a <see cref="ChartWidgetSnapshot"/>.
/// Per-widget permission gate + error isolation already apply upstream
/// (<see cref="IDashboardRenderer"/> / ADR-039 §3); this renderer is
/// responsible for the body shape only.
/// </summary>
internal sealed class ChartWidgetInstanceRenderer(
    ChartService chartService,
    IClock clock) : IWidgetInstanceRenderer
{
    // ConfigJson was written by WidgetDefinitionToInstanceMapper with
    // PropertyNamingPolicy = CamelCase + ChartType / Aggregation as
    // PascalCase strings — JsonStringEnumConverter accepts both string and
    // integer forms.
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

    private readonly ChartService _chartService = chartService;
    private readonly IClock _clock = clock;

    public string WidgetType => "Chart";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        ChartConfig config = JsonSerializer.Deserialize<ChartConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Chart') has empty ConfigJson — chart config cannot be resolved.");

        if (string.IsNullOrWhiteSpace(widget.QueryName))
        {
            throw new InvalidOperationException(
                $"Widget {widget.Id} ('Chart') has no QueryName — Chart widgets must reference a registered QueryDefinition.");
        }

        DateTimeOffset emittedAt = _clock.Now;

        if (!_chartService.TryGetRunner(widget.QueryName, out IChartRunner runner))
        {
            return WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: RefreshHint.Static,
                reasonLocalizationKey: "Widget:Unavailable.QueryNotFound");
        }

        ChartRunnerResult result = await runner
            .ExecuteAsync(config.GroupBy, config.Aggregation, config.Field, context.DashboardFilters, cancellationToken)
            .ConfigureAwait(false);

        ChartWidgetSnapshot snapshot = new(
            ChartType: config.ChartType,
            GroupBy: config.GroupBy,
            Aggregation: config.Aggregation,
            Field: config.Field,
            Buckets: [.. result.Buckets.Select(b => new ChartBucket(b.Label, b.Value))],
            Currency: result.CurrencyCode);

        return WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: emittedAt,
            refreshHint: RefreshHint.Dynamic);
    }

    private sealed record ChartConfig(
        string GroupBy,
        AggregateFunction Aggregation,
        string? Field,
        ChartType ChartType);
}
