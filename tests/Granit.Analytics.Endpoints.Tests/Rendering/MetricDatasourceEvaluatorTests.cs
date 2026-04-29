using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Options;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

public sealed class MetricDatasourceEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 28, 14, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public MetricDatasourceEvaluatorTests() => _clock.Now.Returns(Now);

    [Fact]
    public async Task EvaluateAsync_KnownMetric_BuildsSnapshotFromRunner()
    {
        StubRunner runner = new(
            name: "Granit.Test.UnpaidCount",
            value: 12m,
            supportsPeriod: false,
            valueKind: MetricValueKind.Count,
            isHigherBetter: false,
            refreshHint: RefreshHint.Dynamic);

        MetricDatasourceEvaluator evaluator = BuildEvaluator(runner);

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new MetricDatasource("Granit.Test.UnpaidCount"),
            BuildWidget(),
            BuildContext(period: null),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.ShouldBe(12m);
        result.Payload.ValueKind.ShouldBe(MetricValueKind.Count);
        result.Payload.IsHigherBetter.ShouldBeFalse();
        result.Payload.NoData.ShouldBeFalse();
        result.Payload.Previous.ShouldBeNull();
        result.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        result.UnavailableReasonLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_RunnerReturnsNull_FlagsNoData()
    {
        StubRunner runner = new(
            name: "Granit.Test.AvgRevenue",
            value: null,
            supportsPeriod: true,
            valueKind: MetricValueKind.Currency,
            currencyCode: "EUR",
            isHigherBetter: true,
            refreshHint: RefreshHint.Static);

        MetricDatasourceEvaluator evaluator = BuildEvaluator(runner);

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new MetricDatasource("Granit.Test.AvgRevenue"),
            BuildWidget(),
            BuildContext(period: new ResolvedPeriod(Now.AddDays(-7), Now)),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldNotBeNull();
        result.Payload!.Value.ShouldBeNull();
        result.Payload.NoData.ShouldBeTrue();
        result.Payload.Currency.ShouldBe("EUR");
        result.RefreshHint.ShouldBe(RefreshHint.Static);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownMetric_ReturnsUnavailableWithDedicatedKey()
    {
        StubRunner runner = new("Granit.Test.OtherMetric", value: 1m, supportsPeriod: false);
        MetricDatasourceEvaluator evaluator = BuildEvaluator(runner);

        KpiEvaluation result = await evaluator.EvaluateAsync(
            new MetricDatasource("Granit.Test.MissingMetric"),
            BuildWidget(),
            BuildContext(period: null),
            TestContext.Current.CancellationToken);

        result.Payload.ShouldBeNull();
        result.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.MetricNotFound");
        result.RefreshHint.ShouldBe(RefreshHint.Static);
    }

    [Fact]
    public async Task EvaluateAsync_PeriodAwareRunner_ReceivesContextPeriod()
    {
        ResolvedPeriod expected = new(
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        StubRunner runner = new("Granit.Test.Sum", value: 7m, supportsPeriod: true);
        MetricDatasourceEvaluator evaluator = BuildEvaluator(runner);

        await evaluator.EvaluateAsync(
            new MetricDatasource("Granit.Test.Sum"),
            BuildWidget(),
            BuildContext(period: expected),
            TestContext.Current.CancellationToken);

        runner.LastPeriod.ShouldBe(expected);
    }

    [Fact]
    public async Task EvaluateAsync_RunnerWithoutPeriodSupport_IgnoresContextPeriod()
    {
        // A non-period-aware metric (e.g. a "current open invoices" tile) must not
        // receive the context's period — the runner's own filter pipeline applies
        // unchanged.
        ResolvedPeriod context = new(
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        StubRunner runner = new("Granit.Test.Count", value: 4m, supportsPeriod: false);
        MetricDatasourceEvaluator evaluator = BuildEvaluator(runner);

        await evaluator.EvaluateAsync(
            new MetricDatasource("Granit.Test.Count"),
            BuildWidget(),
            BuildContext(period: context),
            TestContext.Current.CancellationToken);

        runner.LastPeriod.ShouldBeNull();
    }

    private MetricDatasourceEvaluator BuildEvaluator(IMetricRunner runner)
    {
        IFusionCache cache = new FusionCache(
            new FusionCacheOptions { CacheName = "test" },
            new MemoryCache(new MemoryCacheOptions()));

        MetricEndpointService service = new(
            [runner],
            new PeriodResolver(_clock),
            cache,
            _clock,
            Microsoft.Extensions.Options.Options.Create(new AnalyticsEndpointsOptions()),
            _currentTenant);

        return new MetricDatasourceEvaluator(service);
    }

    private static WidgetInstance BuildWidget()
    {
        return WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 1,
            height: 1,
            titleLocalizationKey: "Widget:Test",
            configJson: "{\"kind\":\"metric\",\"metricName\":\"Granit.Test.UnpaidCount\"}");
    }

    private static WidgetRenderContext BuildContext(ResolvedPeriod? period) =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: period,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

    private sealed class StubRunner : IMetricRunner
    {
        private readonly decimal? _value;

        public StubRunner(
            string name,
            decimal? value,
            bool supportsPeriod,
            MetricValueKind valueKind = MetricValueKind.Count,
            string? currencyCode = null,
            bool isHigherBetter = true,
            RefreshHint refreshHint = RefreshHint.Dynamic)
        {
            Name = name;
            _value = value;
            SupportsPeriod = supportsPeriod;
            ValueKind = valueKind;
            CurrencyCode = currencyCode;
            IsHigherBetter = isHigherBetter;
            RefreshHint = refreshHint;
        }

        public string Name { get; }
        public RefreshHint RefreshHint { get; }
        public bool SupportsPeriod { get; }
        public MetricValueKind ValueKind { get; }
        public string? CurrencyCode { get; }
        public bool IsHigherBetter { get; }
        public ResolvedPeriod? LastPeriod { get; private set; }
        public IReadOnlyDictionary<string, string>? LastDashboardFilters { get; private set; }

        public Task<decimal?> ExecuteAsync(
            ResolvedPeriod? period,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            LastPeriod = period;
            LastDashboardFilters = dashboardFilters;
            return Task.FromResult(_value);
        }
    }
}
