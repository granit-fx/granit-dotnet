using Granit.Timeline.AI.Options;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineAIOptionsTests
{
    [Fact]
    public void SectionName_is_Timeline_AI() => TimelineAIOptions.SectionName.ShouldBe("Timeline:AI");

    [Fact]
    public void Default_WorkspaceName_IsNull()
    {
        TimelineAIOptions options = new();
        options.WorkspaceName.ShouldBeNull();
    }

    [Fact]
    public void Default_TimeoutSeconds_Is15()
    {
        TimelineAIOptions options = new();
        options.TimeoutSeconds.ShouldBe(15);
    }

    [Fact]
    public void Default_SummarizerMaxEntries_Is200()
    {
        TimelineAIOptions options = new();
        options.SummarizerMaxEntries.ShouldBe(200);
    }

    [Fact]
    public void Default_AnomalyDetectorMaxEntries_Is500()
    {
        TimelineAIOptions options = new();
        options.AnomalyDetectorMaxEntries.ShouldBe(500);
    }

    [Fact]
    public void Properties_AreMutable()
    {
        TimelineAIOptions options = new()
        {
            WorkspaceName = "custom-workspace",
            TimeoutSeconds = 30,
            SummarizerMaxEntries = 50,
            AnomalyDetectorMaxEntries = 1000,
        };

        options.WorkspaceName.ShouldBe("custom-workspace");
        options.TimeoutSeconds.ShouldBe(30);
        options.SummarizerMaxEntries.ShouldBe(50);
        options.AnomalyDetectorMaxEntries.ShouldBe(1000);
    }
}
