using Granit.Timeline.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class TimelineAIOptionsTests
{
    [Fact]
    public void SectionName_is_AI_Timeline() => TimelineAIOptions.SectionName.ShouldBe("AI:Timeline");

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
    public void Default_MaxEntriesToAnalyze_Is100()
    {
        TimelineAIOptions options = new();
        options.MaxEntriesToAnalyze.ShouldBe(100);
    }

    [Fact]
    public void Properties_AreMutable()
    {
        TimelineAIOptions options = new()
        {
            WorkspaceName = "custom-workspace",
            TimeoutSeconds = 30,
            MaxEntriesToAnalyze = 50,
        };

        options.WorkspaceName.ShouldBe("custom-workspace");
        options.TimeoutSeconds.ShouldBe(30);
        options.MaxEntriesToAnalyze.ShouldBe(50);
    }
}
