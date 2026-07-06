using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineSummaryTests
{

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        DateTimeOffset ts = DateTimeOffset.UtcNow;

        TimelineSummary a = new("Text", 3, ts, ts);
        TimelineSummary b = new("Text", 3, ts, ts);

        a.ShouldBe(b);
    }
}
