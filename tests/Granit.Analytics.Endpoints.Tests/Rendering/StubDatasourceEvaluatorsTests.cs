using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

/// <summary>
/// The framework still ships <see cref="TelemetryDatasourceEvaluator"/> as a
/// pure stub (replaced by <c>Granit.IoT.Dashboards</c> when shipped). The
/// <see cref="QueryAggregateDatasourceEvaluator"/> is fully wired
/// (B3-2bis Count + B3-2ter Sum/Avg/Min/Max via field-selector reflection);
/// these tests pin the no-runner-registered branch and the empty-set semantics
/// at the evaluator layer, complementing
/// <see cref="Internal.QueryAggregateRunnerTests"/> which exercises the EF
/// Core path end-to-end.
/// </summary>
public sealed class StubDatasourceEvaluatorsTests
{
    private static WidgetInstance BuildWidget(string widgetType) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: widgetType,
            position: 0,
            width: 1,
            height: 1,
            titleLocalizationKey: "Widget:Test",
            configJson: "{}");

    private static WidgetRenderContext BuildContext() =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: null,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

    [Fact]
    public async Task QueryAggregateEvaluator_UnknownQueryName_ReturnsUnavailableNotFound()
    {
        // No runners registered — every query name resolves to "not found".
        QueryAggregateDatasourceEvaluator evaluator = new(new QueryAggregateService([]));

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Invoicing.InvoiceQuery",
                Aggregation: AggregateFunction.Count),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldBeNull();
        result.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryAggregateNotFound");
        result.RefreshHint.ShouldBe(RefreshHint.Static);
    }

    [Fact]
    public async Task QueryAggregateEvaluator_Count_BuildsCountSnapshot()
    {
        StubRunner runner = new("Granit.Test.Query", value: 7m);
        QueryAggregateDatasourceEvaluator evaluator = new(new QueryAggregateService([runner]));

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Test.Query",
                Aggregation: AggregateFunction.Count),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.ShouldBe(7m);
        result.Payload.ValueKind.ShouldBe(MetricValueKind.Count);
        result.Payload.NoData.ShouldBeFalse();
        result.Payload.IsHigherBetter.ShouldBeTrue();
        result.RefreshHint.ShouldBe(RefreshHint.Dynamic);
    }

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task QueryAggregateEvaluator_NumericAggregation_BuildsNumberSnapshot(AggregateFunction aggregation)
    {
        StubRunner runner = new("Granit.Test.Query", value: 42m);
        QueryAggregateDatasourceEvaluator evaluator = new(new QueryAggregateService([runner]));

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Test.Query",
                Aggregation: aggregation,
                Field: "Amount"),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.ShouldBe(42m);
        result.Payload.ValueKind.ShouldBe(MetricValueKind.Number);
        result.Payload.NoData.ShouldBeFalse();
    }

    [Theory]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task QueryAggregateEvaluator_AvgMinMaxOnEmptySet_SurfacesNoData(AggregateFunction aggregation)
    {
        // Avg / Min / Max over an empty set return null from the runner — the
        // evaluator must surface that as NoData=true on a Snapshot envelope
        // (NOT Unavailable). The frontend renders "—"; the widget is still a
        // first-class result, just with no measurement to display.
        StubRunner runner = new("Granit.Test.Query", value: null);
        QueryAggregateDatasourceEvaluator evaluator = new(new QueryAggregateService([runner]));

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Test.Query",
                Aggregation: aggregation,
                Field: "Amount"),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.ShouldBeNull();
        result.Payload.NoData.ShouldBeTrue();
        result.Payload.ValueKind.ShouldBe(MetricValueKind.Number);
    }

    private sealed class StubRunner(string name, decimal? value) : IQueryAggregateRunner
    {
        public string Name { get; } = name;

        public Task<decimal?> ExecuteAsync(
            AggregateFunction aggregation,
            string? field,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken) =>
            Task.FromResult(value);
    }

    [Fact]
    public async Task TelemetryEvaluator_ReturnsUnavailableWithDedicatedKey()
    {
        TelemetryDatasourceEvaluator evaluator = new();

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new TelemetryDatasource(
                EntityAlias: "currentDevice",
                TelemetryKey: "temperature"),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldBeNull();
        result.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.TelemetryNotImplemented");
        result.RefreshHint.ShouldBe(RefreshHint.Static);
    }
}
