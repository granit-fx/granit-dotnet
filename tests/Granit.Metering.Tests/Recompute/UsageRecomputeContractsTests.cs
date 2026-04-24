using Granit.Metering.Recompute;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests.Recompute;

/// <summary>
/// Sanity tests for the recompute contracts (request, result, rejection exception).
/// The full <see cref="IUsageRecomputeService"/> behavior is exercised in the EF Core
/// project's integration suite where a transaction-aware provider is available.
/// </summary>
public sealed class UsageRecomputeContractsTests
{
    [Fact]
    public void UsageRecomputeRequest_KeepsSuppliedValues()
    {
        var meterId = Guid.NewGuid();
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(-6);

        UsageRecomputeRequest request = new(meterId, from, to);

        request.MeterDefinitionId.ShouldBe(meterId);
        request.From.ShouldBe(from);
        request.To.ShouldBe(to);
    }

    [Fact]
    public void UsageRecomputeResult_KeepsSuppliedValues()
    {
        var meterId = Guid.NewGuid();
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-2);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(-1);

        UsageRecomputeResult result = new(
            meterId, from, to,
            EventsScanned: 1234,
            AggregatesRebuilt: 24,
            DurationMilliseconds: 87);

        result.MeterDefinitionId.ShouldBe(meterId);
        result.WindowStart.ShouldBe(from);
        result.WindowEnd.ShouldBe(to);
        result.EventsScanned.ShouldBe(1234);
        result.AggregatesRebuilt.ShouldBe(24);
        result.DurationMilliseconds.ShouldBe(87);
    }

    [Fact]
    public void UsageRecomputeRejectedException_ExposesReasonCode()
    {
        UsageRecomputeRejectedException ex = new(
            "Granit:Metering:RecomputeMeterArchived",
            "Meter is archived.");

        ex.ReasonCode.ShouldBe("Granit:Metering:RecomputeMeterArchived");
        ex.Message.ShouldBe("Meter is archived.");
    }
}
