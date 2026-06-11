using Granit.Validation.AI.Options;
using Shouldly;

namespace Granit.Validation.AI.Tests;

public sealed class ValidationAIOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() => ValidationAIOptions.SectionName.ShouldBe("Validation:AI");

    [Fact]
    public void WorkspaceName_DefaultValue_IsDefault()
    {
        ValidationAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void TimeoutSeconds_DefaultValue_Is2()
    {
        ValidationAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(2);
    }

    [Fact]
    public void SeverityThreshold_DefaultValue_Is05()
    {
        ValidationAIOptions options = new();

        options.SeverityThreshold.ShouldBe(0.5);
    }

    [Fact]
    public void AllProperties_CanBeChanged()
    {
        ValidationAIOptions options = new()
        {
            WorkspaceName = "custom",
            TimeoutSeconds = 5,
            SeverityThreshold = 0.8,
        };

        options.WorkspaceName.ShouldBe("custom");
        options.TimeoutSeconds.ShouldBe(5);
        options.SeverityThreshold.ShouldBe(0.8);
    }
}
