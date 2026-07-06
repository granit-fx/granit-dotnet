using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class AnomalyReportTests
{

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        List<TimelineAnomaly> anomalies = [new("Test", "Low")];

        AnomalyReport a = new(true, anomalies);
        AnomalyReport b = new(true, anomalies);

        a.ShouldBe(b);
    }
}
