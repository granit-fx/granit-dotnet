using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineSummaryTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        DateTimeOffset oldest = DateTimeOffset.UtcNow.AddHours(-2);
        DateTimeOffset newest = DateTimeOffset.UtcNow;

        TimelineSummary summary = new("Summary text", 5, oldest, newest);

        summary.Text.ShouldBe("Summary text");
        summary.EntryCount.ShouldBe(5);
        summary.OldestEntry.ShouldBe(oldest);
        summary.NewestEntry.ShouldBe(newest);
    }

    [Fact]
    public void NullTimestamps_AreValid()
    {
        TimelineSummary summary = new("No entries", 0, null, null);

        summary.OldestEntry.ShouldBeNull();
        summary.NewestEntry.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        DateTimeOffset ts = DateTimeOffset.UtcNow;

        TimelineSummary a = new("Text", 3, ts, ts);
        TimelineSummary b = new("Text", 3, ts, ts);

        a.ShouldBe(b);
    }
}
