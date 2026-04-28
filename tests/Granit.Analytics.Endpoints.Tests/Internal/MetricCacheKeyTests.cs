using Granit.Analytics.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

public sealed class MetricCacheKeyTests
{
    private static readonly ResolvedPeriod TestPeriod = new(
        new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Compose_WithTenant_IncludesTenantId()
    {
        string key = MetricCacheKey.Compose("Test.Metric", "tenant-a", TestPeriod, compareTo: null);

        key.ShouldContain("tenant-a");
        key.ShouldStartWith("analytics:metric:Test.Metric");
    }

    [Fact]
    public void Compose_WithoutTenant_FallsBackToGlobal()
    {
        string key = MetricCacheKey.Compose("Test.Metric", tenantId: null, TestPeriod, compareTo: null);

        key.ShouldContain(":global:");
    }

    [Fact]
    public void Compose_DifferentTenants_ProducesDifferentKeys()
    {
        // Critical: cross-tenant cache contamination is the #1 risk.
        string keyA = MetricCacheKey.Compose("Test.Metric", "tenant-a", TestPeriod, compareTo: null);
        string keyB = MetricCacheKey.Compose("Test.Metric", "tenant-b", TestPeriod, compareTo: null);

        keyA.ShouldNotBe(keyB);
    }

    [Fact]
    public void Compose_DifferentPeriods_ProducesDifferentKeys()
    {
        ResolvedPeriod other = new(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

        string keyA = MetricCacheKey.Compose("Test.Metric", "t", TestPeriod, compareTo: null);
        string keyB = MetricCacheKey.Compose("Test.Metric", "t", other, compareTo: null);

        keyA.ShouldNotBe(keyB);
    }

    [Fact]
    public void Compose_WithComparison_DistinguishedFromMain()
    {
        string mainKey = MetricCacheKey.Compose("Test.Metric", "t", TestPeriod, compareTo: null);
        string compareKey = MetricCacheKey.Compose("Test.Metric", "t", period: null, compareTo: TestPeriod);

        mainKey.ShouldNotBe(compareKey);
    }
}
