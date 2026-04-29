using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Options;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Exceptions;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Analytics.Endpoints.Tests.Internal;

public sealed class MetricEndpointServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 28, 14, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public MetricEndpointServiceTests()
    {
        _clock.Now.Returns(Now);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEnvelope_WithSnapshotAndMetadata()
    {
        StubRunner runner = new("Test.Count", returns: 42m, supportsPeriod: false);
        MetricEndpointService service = BuildService([runner]);

        MetricResponse response = await service.EvaluateAsync(
            runner,
            new MetricRequest(new PeriodSpec(Token: "today")),
            TestContext.Current.CancellationToken);

        response.Name.ShouldBe("Test.Count");
        response.Snapshot.Value.ShouldBe(42m);
        response.Snapshot.NoData.ShouldBeFalse();
        response.Snapshot.Previous.ShouldBeNull();
        response.Sequence.ShouldBe(1);
        response.EmittedAt.ShouldBe(Now);
        response.RefreshHint.ShouldBe(RefreshHint.Dynamic);
    }

    [Fact]
    public async Task EvaluateAsync_NoData_SetsNoDataAndNullValue()
    {
        StubRunner runner = new("Test.Average", returns: null, supportsPeriod: false);
        MetricEndpointService service = BuildService([runner]);

        MetricResponse response = await service.EvaluateAsync(
            runner,
            new MetricRequest(new PeriodSpec(Token: "today")),
            TestContext.Current.CancellationToken);

        response.Snapshot.Value.ShouldBeNull();
        response.Snapshot.NoData.ShouldBeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithCompareTo_OnPeriodAwareMetric_PopulatesPreviousAndDelta()
    {
        QueueRunner runner = new("Test.Revenue", supportsPeriod: true);
        runner.Enqueue(110m); // current
        runner.Enqueue(100m); // previous

        MetricEndpointService service = BuildService([runner]);

        DateTimeOffset from = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        MetricResponse response = await service.EvaluateAsync(
            runner,
            new MetricRequest(
                Period: new PeriodSpec(from, to),
                CompareTo: new PeriodSpec(Token: "previous_period")),
            TestContext.Current.CancellationToken);

        response.Snapshot.Value.ShouldBe(110m);
        response.Snapshot.Previous.ShouldNotBeNull();
        response.Snapshot.Previous!.Value.ShouldBe(100m);
        response.Snapshot.Previous.Trend.ShouldBe("up");
        response.Snapshot.Previous.IsFavorable.ShouldBe(true);
    }

    [Fact]
    public async Task EvaluateAsync_CompareTo_OnNonPeriodAwareMetric_Throws()
    {
        StubRunner runner = new("Test.Count", returns: 42m, supportsPeriod: false);
        MetricEndpointService service = BuildService([runner]);

        await Should.ThrowAsync<BusinessRuleViolationException>(async () =>
            await service.EvaluateAsync(
                runner,
                new MetricRequest(
                    Period: new PeriodSpec(Token: "today"),
                    CompareTo: new PeriodSpec(Token: "previous_period")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EvaluateAsync_SecondCallWithSameTenantAndPeriod_HitsCache()
    {
        QueueRunner runner = new("Test.Count", supportsPeriod: false);
        runner.Enqueue(11m);
        runner.Enqueue(99m); // would only be returned if executor is re-invoked

        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(Guid.NewGuid());

        MetricEndpointService service = BuildService([runner]);

        MetricResponse first = await service.EvaluateAsync(
            runner, new MetricRequest(new PeriodSpec(Token: "today")), TestContext.Current.CancellationToken);
        MetricResponse second = await service.EvaluateAsync(
            runner, new MetricRequest(new PeriodSpec(Token: "today")), TestContext.Current.CancellationToken);

        first.Snapshot.Value.ShouldBe(11m);
        second.Snapshot.Value.ShouldBe(11m); // cache hit — second value (99) NOT returned
        runner.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_DifferentTenants_DoNotShareCache()
    {
        // The cache key must include tenantId — without it, a tenant could read another tenant's value.
        QueueRunner runner = new("Test.Count", supportsPeriod: false);
        runner.Enqueue(10m); // tenant A
        runner.Enqueue(20m); // tenant B

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        MetricEndpointService service = BuildService([runner]);

        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantA);
        MetricResponse responseA = await service.EvaluateAsync(
            runner, new MetricRequest(new PeriodSpec(Token: "today")), TestContext.Current.CancellationToken);

        _currentTenant.Id.Returns(tenantB);
        MetricResponse responseB = await service.EvaluateAsync(
            runner, new MetricRequest(new PeriodSpec(Token: "today")), TestContext.Current.CancellationToken);

        responseA.Snapshot.Value.ShouldBe(10m);
        responseB.Snapshot.Value.ShouldBe(20m);
        runner.CallCount.ShouldBe(2);
    }

    [Fact]
    public void TryGetRunner_UnknownName_ReturnsFalse()
    {
        StubRunner runner = new("Test.Count", returns: 0m, supportsPeriod: false);
        MetricEndpointService service = BuildService([runner]);

        bool found = service.TryGetRunner("Unknown.Metric", out IMetricRunner _);
        found.ShouldBeFalse();
    }

    [Fact]
    public void TryGetRunner_KnownName_ReturnsTrueAndRunner()
    {
        StubRunner runner = new("Test.Count", returns: 0m, supportsPeriod: false);
        MetricEndpointService service = BuildService([runner]);

        bool found = service.TryGetRunner("Test.Count", out IMetricRunner resolved);
        found.ShouldBeTrue();
        resolved.ShouldBeSameAs(runner);
    }

    private MetricEndpointService BuildService(IMetricRunner[] runners)
    {
        IFusionCache cache = new FusionCache(new FusionCacheOptions { CacheName = "test" }, new MemoryCache(new MemoryCacheOptions()));
        return new MetricEndpointService(
            runners,
            new PeriodResolver(_clock),
            cache,
            _clock,
            Microsoft.Extensions.Options.Options.Create(new AnalyticsEndpointsOptions()),
            _currentTenant);
    }

    private sealed class StubRunner(string name, decimal? returns, bool supportsPeriod) : IMetricRunner
    {
        public string Name { get; } = name;
        public RefreshHint RefreshHint => RefreshHint.Dynamic;
        public bool SupportsPeriod { get; } = supportsPeriod;
        public MetricValueKind ValueKind => MetricValueKind.Count;
        public string? CurrencyCode => null;
        public bool IsHigherBetter => true;

        public Task<decimal?> ExecuteAsync(
            ResolvedPeriod? period,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken) =>
            Task.FromResult(returns);
    }

    private sealed class QueueRunner(string name, bool supportsPeriod) : IMetricRunner
    {
        private readonly Queue<decimal?> _values = new();
        public int CallCount { get; private set; }

        public string Name { get; } = name;
        public RefreshHint RefreshHint => RefreshHint.Dynamic;
        public bool SupportsPeriod { get; } = supportsPeriod;
        public MetricValueKind ValueKind => MetricValueKind.Count;
        public string? CurrencyCode => null;
        public bool IsHigherBetter => true;

        public void Enqueue(decimal? value) => _values.Enqueue(value);

        public Task<decimal?> ExecuteAsync(
            ResolvedPeriod? period,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_values.Dequeue());
        }
    }
}
