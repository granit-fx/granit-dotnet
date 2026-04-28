using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

public sealed class DeltaCalculatorTests
{
    [Fact]
    public void HigherIsBetter_PositiveTrend_IsFavorable()
    {
        MetricPreviousPayload result = DeltaCalculator.Build(current: 110m, previous: 100m, isHigherBetter: true);

        result.Trend.ShouldBe("up");
        result.IsFavorable.ShouldBe(true);
        result.DeltaRatio.ShouldNotBeNull();
        result.DeltaRatio!.Value.ShouldBe(0.10, 1e-9);
    }

    [Fact]
    public void HigherIsBetter_NegativeTrend_IsUnfavorable()
    {
        MetricPreviousPayload result = DeltaCalculator.Build(current: 90m, previous: 100m, isHigherBetter: true);

        result.Trend.ShouldBe("down");
        result.IsFavorable.ShouldBe(false);
    }

    [Fact]
    public void LowerIsBetter_NegativeTrend_IsFavorable()
    {
        // E.g. unpaid invoice count going down is good.
        MetricPreviousPayload result = DeltaCalculator.Build(current: 8m, previous: 12m, isHigherBetter: false);

        result.Trend.ShouldBe("down");
        result.IsFavorable.ShouldBe(true);
    }

    [Fact]
    public void Equal_TrendIsFlat_FavorableTrue()
    {
        MetricPreviousPayload result = DeltaCalculator.Build(current: 100m, previous: 100m, isHigherBetter: true);

        result.Trend.ShouldBe("flat");
        result.IsFavorable.ShouldBe(true);
        result.DeltaRatio!.Value.ShouldBe(0.0, 1e-9);
    }

    [Fact]
    public void PreviousIsZero_DeltaRatioIsNull()
    {
        MetricPreviousPayload result = DeltaCalculator.Build(current: 50m, previous: 0m, isHigherBetter: true);

        result.DeltaRatio.ShouldBeNull();
        result.Trend.ShouldBe("up");
    }

    [Fact]
    public void CurrentIsNull_TrendIsFlatFavorableNull()
    {
        MetricPreviousPayload result = DeltaCalculator.Build(current: null, previous: 100m, isHigherBetter: true);

        result.Trend.ShouldBe("flat");
        result.IsFavorable.ShouldBeNull();
        result.DeltaRatio.ShouldBeNull();
    }

    [Fact]
    public void NegativePreviousValue_DeltaRatioUsesAbsolute()
    {
        // Deltas should be relative to magnitude — going from -100 to -80 is a 20% improvement.
        MetricPreviousPayload result = DeltaCalculator.Build(current: -80m, previous: -100m, isHigherBetter: true);

        result.Trend.ShouldBe("up");
        result.IsFavorable.ShouldBe(true);
        result.DeltaRatio!.Value.ShouldBe(0.20, 1e-9);
    }
}
