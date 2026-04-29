using System.Security.Claims;
using Granit.Analytics;
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
/// The framework ships <see cref="QueryAggregateDatasourceEvaluator"/> and
/// <see cref="TelemetryDatasourceEvaluator"/> as stubs (the full implementations
/// land in follow-up slices / <c>Granit.IoT.Dashboards</c>). The contract for
/// today: never throw, always surface a localizable
/// <c>Widget:Unavailable.*</c> reason key — so a dashboard bound to either kind
/// renders a typed "unavailable" widget instead of a 500.
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
    public async Task QueryAggregateEvaluator_ReturnsUnavailableWithDedicatedKey()
    {
        QueryAggregateDatasourceEvaluator evaluator = new();

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new QueryAggregateDatasource(
                QueryName: "Granit.Invoicing.InvoiceQuery",
                Aggregation: AggregateFunction.Count),
            BuildWidget("Kpi"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldBeNull();
        result.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryAggregateNotImplemented");
        result.RefreshHint.ShouldBe(RefreshHint.Static);
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
