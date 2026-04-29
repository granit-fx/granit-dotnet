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
/// pure stub (replaced by <c>Granit.IoT.Dashboards</c> when shipped) and
/// <see cref="QueryAggregateDatasourceEvaluator"/> as a partial — Count is
/// fully wired (B3-2bis), Sum / Avg / Min / Max land in a follow-up slice.
/// Both contracts: never throw, always surface a localizable
/// <c>Widget:Unavailable.*</c> reason key when the path is not (yet)
/// implemented.
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

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    public async Task QueryAggregateEvaluator_NonCountAggregation_ReturnsUnavailableOperationNotImplemented(AggregateFunction aggregation)
    {
        // Runner is registered but returns null for non-Count aggregations
        // (Sum / Avg / Min / Max ship in a follow-up slice).
        StubRunner runner = new("Granit.Test.Query", returnsForCount: 1m);
        QueryAggregateDatasourceEvaluator evaluator = new(new QueryAggregateService([runner]));

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Test.Query",
                Aggregation: aggregation,
                Field: "Amount"),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldBeNull();
        result.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryAggregateOperationNotImplemented");
    }

    [Fact]
    public async Task QueryAggregateEvaluator_Count_BuildsCountSnapshot()
    {
        StubRunner runner = new("Granit.Test.Query", returnsForCount: 7m);
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

    private sealed class StubRunner(string name, decimal? returnsForCount) : IQueryAggregateRunner
    {
        public string Name { get; } = name;

        public Task<decimal?> ExecuteAsync(
            AggregateFunction aggregation,
            string? field,
            CancellationToken cancellationToken) =>
            Task.FromResult(aggregation == AggregateFunction.Count ? returnsForCount : (decimal?)null);
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
