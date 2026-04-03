using Granit.Metering.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests;

public sealed class QuotaStatusTests
{
    [Fact]
    public void Unlimited_ShouldReturnNoLimit()
    {
        var status = QuotaStatus.Unlimited("API Calls", 500m);

        status.MeterName.ShouldBe("API Calls");
        status.CurrentUsage.ShouldBe(500m);
        status.Limit.ShouldBeNull();
        status.PercentUsed.ShouldBeNull();
        status.IsExceeded.ShouldBeFalse();
    }

    [Fact]
    public void WithLimit_BelowLimit_ShouldNotBeExceeded()
    {
        var status = QuotaStatus.WithLimit("API Calls", 800m, 1000m);

        status.PercentUsed.ShouldBe(80m);
        status.IsExceeded.ShouldBeFalse();
    }

    [Fact]
    public void WithLimit_AtLimit_ShouldBeExceeded()
    {
        var status = QuotaStatus.WithLimit("API Calls", 1000m, 1000m);

        status.PercentUsed.ShouldBe(100m);
        status.IsExceeded.ShouldBeTrue();
    }

    [Fact]
    public void WithLimit_AboveLimit_ShouldBeExceeded()
    {
        var status = QuotaStatus.WithLimit("API Calls", 1200m, 1000m);

        status.PercentUsed.ShouldBe(120m);
        status.IsExceeded.ShouldBeTrue();
    }

    [Fact]
    public void WithLimit_ZeroLimit_ShouldBeExceeded()
    {
        var status = QuotaStatus.WithLimit("API Calls", 1m, 0m);

        status.PercentUsed.ShouldBe(100m);
        status.IsExceeded.ShouldBeTrue();
    }

    [Fact]
    public void WithLimit_ZeroUsage_ShouldNotBeExceeded()
    {
        var status = QuotaStatus.WithLimit("API Calls", 0m, 1000m);

        status.PercentUsed.ShouldBe(0m);
        status.IsExceeded.ShouldBeFalse();
    }
}
