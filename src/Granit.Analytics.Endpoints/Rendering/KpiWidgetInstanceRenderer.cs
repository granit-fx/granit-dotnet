using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Kpi"</c> widget kind.
/// Deserialises the persisted <see cref="WidgetInstance.ConfigJson"/> into a
/// polymorphic <see cref="Datasource"/> and dispatches to the matching
/// <see cref="IDatasourceEvaluator{TDatasource}"/> — ADR-039 §7.bis. Adding a
/// new datasource kind is purely additive (new evaluator + DI registration);
/// the renderer's switch never grows.
/// </summary>
internal sealed class KpiWidgetInstanceRenderer(
    IDatasourceEvaluator<MetricDatasource> metricEvaluator,
    IDatasourceEvaluator<QueryAggregateDatasource> queryEvaluator,
    IDatasourceEvaluator<TelemetryDatasource> telemetryEvaluator,
    IClock clock) : IWidgetInstanceRenderer
{
    // The persisted ConfigJson was written by WidgetDefinitionToInstanceMapper with
    // PropertyNamingPolicy = CamelCase — must round-trip with the same policy.
    // The polymorphism discriminator ("kind") is fixed by [JsonPolymorphic] and
    // is unaffected by the naming policy. JsonStringEnumConverter accepts both
    // string and integer enum values, so the renderer reads either form (forward-
    // compatible if the mapper switches to string enums later).
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    // Snapshot serialisation: camelCase properties + PascalCase enum members
    // (matches host JsonStringEnumConverter() default, locked by ADR-039 §6.1).
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDatasourceEvaluator<MetricDatasource> _metricEvaluator = metricEvaluator;
    private readonly IDatasourceEvaluator<QueryAggregateDatasource> _queryEvaluator = queryEvaluator;
    private readonly IDatasourceEvaluator<TelemetryDatasource> _telemetryEvaluator = telemetryEvaluator;
    private readonly IClock _clock = clock;

    public string WidgetType => "Kpi";

    public async Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(context);

        Datasource datasource = JsonSerializer.Deserialize<Datasource>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Kpi') has an empty or null ConfigJson — datasource cannot be resolved.");

        KpiEvaluation evaluation = datasource switch
        {
            MetricDatasource m => await _metricEvaluator.EvaluateAsync(m, widget, context, cancellationToken).ConfigureAwait(false),
            QueryAggregateDatasource q => await _queryEvaluator.EvaluateAsync(q, widget, context, cancellationToken).ConfigureAwait(false),
            TelemetryDatasource t => await _telemetryEvaluator.EvaluateAsync(t, widget, context, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException(
                $"Unknown KPI datasource kind '{datasource.GetType().Name}' on widget {widget.Id}."),
        };

        DateTimeOffset emittedAt = _clock.Now;

        return evaluation.Payload is { } payload
            ? WidgetSnapshotEnvelope.ForSnapshot(
                widgetType: WidgetType,
                snapshot: JsonSerializer.SerializeToElement(payload, SnapshotJsonOptions),
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: evaluation.RefreshHint)
            : WidgetSnapshotEnvelope.Unavailable(
                widgetType: WidgetType,
                sequence: 1,
                emittedAt: emittedAt,
                refreshHint: evaluation.RefreshHint,
                reasonLocalizationKey: evaluation.UnavailableReasonLocalizationKey ?? "Widget:Unavailable");
    }
}
