using Granit.Templating.AI.Options;

namespace Granit.Templating.AI.Tests;

public sealed class TemplatingAIOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() => TemplatingAIOptions.SectionName.ShouldBe("Templating:AI");

    [Fact]
    public void Defaults_AreCorrect()
    {
        TemplatingAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void WorkspaceName_CanBeSet()
    {
        TemplatingAIOptions options = new()
        {
            WorkspaceName = "custom-workspace",
        };

        options.WorkspaceName.ShouldBe("custom-workspace");
    }

    [Fact]
    public void TimeoutSeconds_CanBeSet()
    {
        TemplatingAIOptions options = new()
        {
            TimeoutSeconds = 60,
        };

        options.TimeoutSeconds.ShouldBe(60);
    }
}
