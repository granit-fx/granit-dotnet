using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class AnomalyReportTests
{
    [Fact]
    public void NoAnomalies_HasAnomaliesIsFalse()
    {
        AnomalyReport report = new(false, []);

        report.HasAnomalies.ShouldBeFalse();
        report.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void WithAnomalies_HasAnomaliesIsTrue()
    {
        List<TimelineAnomaly> anomalies = [new("Bulk edits detected", "Medium")];

        AnomalyReport report = new(true, anomalies);

        report.HasAnomalies.ShouldBeTrue();
        report.Anomalies.Count.ShouldBe(1);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        List<TimelineAnomaly> anomalies = [new("Test", "Low")];

        AnomalyReport a = new(true, anomalies);
        AnomalyReport b = new(true, anomalies);

        a.ShouldBe(b);
    }
}
