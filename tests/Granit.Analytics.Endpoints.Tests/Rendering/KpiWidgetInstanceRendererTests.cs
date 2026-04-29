using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

/// <summary>
/// Locks the dispatch contract for <see cref="KpiWidgetInstanceRenderer"/> per
/// ADR-039 §7.bis — the renderer parses <c>WidgetInstance.ConfigJson</c> as a
/// polymorphic <c>Datasource</c>, routes to the matching
/// <see cref="IDatasourceEvaluator{TDatasource}"/>, and shapes the result into
/// the locked-v1 <see cref="WidgetSnapshotEnvelope"/> wire shape.
/// </summary>
public sealed class KpiWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 28, 14, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RecordingEvaluator<MetricDatasource> _metricEvaluator = new();
    private readonly RecordingEvaluator<QueryAggregateDatasource> _queryEvaluator = new();
    private readonly RecordingEvaluator<TelemetryDatasource> _telemetryEvaluator = new();

    public KpiWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsKpi()
    {
        BuildRenderer().WidgetType.ShouldBe("Kpi");
    }

    [Fact]
    public async Task RenderAsync_DispatchesMetricDatasource_ToMetricEvaluator()
    {
        _metricEvaluator.Result = KpiEvaluation.Snapshot(
            new MetricSnapshotPayload(
                Value: 42m,
                ValueKind: MetricValueKind.Count,
                Currency: null,
                IsHigherBetter: true,
                NoData: false,
                Previous: null),
            RefreshHint.Dynamic);

        WidgetSnapshotEnvelope envelope = await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"metric\",\"metricName\":\"Granit.Test.Count\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        _metricEvaluator.LastDatasource.ShouldNotBeNull();
        _metricEvaluator.LastDatasource!.MetricName.ShouldBe("Granit.Test.Count");
        _queryEvaluator.LastDatasource.ShouldBeNull();
        _telemetryEvaluator.LastDatasource.ShouldBeNull();

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Kpi");
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        envelope.EmittedAt.ShouldBe(Now);
        envelope.Sequence.ShouldBe(1);
        envelope.ReasonLocalizationKey.ShouldBeNull();

        envelope.Snapshot.ShouldNotBeNull();
        envelope.Snapshot!.Value.GetProperty("value").GetDecimal().ShouldBe(42m);
        envelope.Snapshot.Value.GetProperty("noData").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task RenderAsync_DispatchesQueryAggregateDatasource_ToQueryEvaluator()
    {
        _queryEvaluator.Result = KpiEvaluation.Unavailable(
            RefreshHint.Static,
            "Widget:Unavailable.QueryAggregateNotImplemented");

        WidgetSnapshotEnvelope envelope = await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"query-aggregate\",\"queryName\":\"Granit.Invoicing.InvoiceQuery\",\"aggregation\":\"Count\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        _queryEvaluator.LastDatasource.ShouldNotBeNull();
        _queryEvaluator.LastDatasource!.QueryName.ShouldBe("Granit.Invoicing.InvoiceQuery");
        _queryEvaluator.LastDatasource.Aggregation.ShouldBe(AggregateFunction.Count);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Kpi");
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryAggregateNotImplemented");
        envelope.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task RenderAsync_DispatchesTelemetryDatasource_ToTelemetryEvaluator()
    {
        _telemetryEvaluator.Result = KpiEvaluation.Unavailable(
            RefreshHint.Static,
            "Widget:Unavailable.TelemetryNotImplemented");

        WidgetSnapshotEnvelope envelope = await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"iot-telemetry\",\"entityAlias\":\"currentDevice\",\"telemetryKey\":\"temperature\",\"aggregation\":\"Last\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        _telemetryEvaluator.LastDatasource.ShouldNotBeNull();
        _telemetryEvaluator.LastDatasource!.EntityAlias.ShouldBe("currentDevice");
        _telemetryEvaluator.LastDatasource.TelemetryKey.ShouldBe("temperature");

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.TelemetryNotImplemented");
    }

    [Fact]
    public async Task RenderAsync_UnavailableEvaluation_DefaultsReasonKey_WhenEvaluatorOmitsIt()
    {
        // Defensive — KpiEvaluation.Unavailable enforces a non-null key, but the
        // renderer must still default if a future evaluator forgets one (the
        // public surface of the contract).
        _metricEvaluator.Result = new KpiEvaluation(
            Payload: null,
            RefreshHint: RefreshHint.Static,
            ReasonLocalizationKey: null);

        WidgetSnapshotEnvelope envelope = await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"metric\",\"metricName\":\"Granit.Test.Count\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable");
    }

    [Fact]
    public async Task RenderAsync_PassesRenderContext_Through_ToEvaluator()
    {
        _metricEvaluator.Result = KpiEvaluation.Snapshot(
            new MetricSnapshotPayload(0m, MetricValueKind.Count, null, true, false, null),
            RefreshHint.Dynamic);

        WidgetRenderContext context = BuildContext(
            period: new ResolvedPeriod(Now.AddDays(-7), Now));

        await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"metric\",\"metricName\":\"Granit.Test.Count\"}"),
            context,
            TestContext.Current.CancellationToken);

        _metricEvaluator.LastContext.ShouldBeSameAs(context);
    }

    [Fact]
    public async Task RenderAsync_SnapshotPayload_SerialisesAsCamelCase()
    {
        // Locks ADR-039 §6.1 wire convention for the inner snapshot — camelCase
        // properties + PascalCase enum members. The frontend's discriminated-union
        // types depend on this exact shape.
        _metricEvaluator.Result = KpiEvaluation.Snapshot(
            new MetricSnapshotPayload(
                Value: 1234.56m,
                ValueKind: MetricValueKind.Currency,
                Currency: "EUR",
                IsHigherBetter: true,
                NoData: false,
                Previous: null),
            RefreshHint.Dynamic);

        WidgetSnapshotEnvelope envelope = await BuildRenderer().RenderAsync(
            BuildWidget("{\"kind\":\"metric\",\"metricName\":\"Granit.Test.Revenue\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Snapshot.ShouldNotBeNull();
        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"value\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"valueKind\":\"Currency\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"currency\":\"EUR\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"isHigherBetter\":true", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"noData\":false", StringComparison.Ordinal).ShouldBeTrue(raw);
    }

    private KpiWidgetInstanceRenderer BuildRenderer() =>
        new(_metricEvaluator, _queryEvaluator, _telemetryEvaluator, _clock);

    private static WidgetInstance BuildWidget(string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 1,
            height: 1,
            titleLocalizationKey: "Widget:Test",
            configJson: configJson);

    private static WidgetRenderContext BuildContext(ResolvedPeriod? period = null) =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: period,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

    private sealed class RecordingEvaluator<TDatasource> : IDatasourceEvaluator<TDatasource>
        where TDatasource : Datasource
    {
        public TDatasource? LastDatasource { get; private set; }

        public WidgetRenderContext? LastContext { get; private set; }

        public KpiEvaluation Result { get; set; } = KpiEvaluation.Unavailable(
            RefreshHint.Static, "Widget:Unavailable.TestDefault");

        public Task<KpiEvaluation> EvaluateAsync(
            TDatasource datasource,
            WidgetInstance widget,
            WidgetRenderContext context,
            CancellationToken cancellationToken)
        {
            LastDatasource = datasource;
            LastContext = context;
            return Task.FromResult(Result);
        }
    }
}
