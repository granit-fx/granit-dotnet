using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineAnomalyTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        TimelineAnomaly anomaly = new("Off-hours activity", "High");

        anomaly.Description.ShouldBe("Off-hours activity");
        anomaly.Severity.ShouldBe("High");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        TimelineAnomaly a = new("Test", "Low");
        TimelineAnomaly b = new("Test", "Low");

        a.ShouldBe(b);
    }

    [Fact]
    public void RecordInequality_DetectsDifferences()
    {
        TimelineAnomaly a = new("Test", "Low");
        TimelineAnomaly b = new("Test", "High");

        a.ShouldNotBe(b);
    }
}
