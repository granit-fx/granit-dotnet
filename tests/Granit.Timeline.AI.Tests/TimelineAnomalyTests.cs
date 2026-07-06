using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineAnomalyTests
{

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
