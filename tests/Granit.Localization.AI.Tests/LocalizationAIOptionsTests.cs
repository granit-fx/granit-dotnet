using Granit.Localization.AI.Options;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class LocalizationAIOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => LocalizationAIOptions.SectionName.ShouldBe("AI:Localization");

    [Fact]
    public void WorkspaceName_DefaultsToDefault()
    {
        LocalizationAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void WorkspaceName_CanBeChanged()
    {
        LocalizationAIOptions options = new()
        {
            WorkspaceName = "translation-workspace",
        };

        options.WorkspaceName.ShouldBe("translation-workspace");
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        LocalizationAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void TimeoutSeconds_CanBeChanged()
    {
        LocalizationAIOptions options = new()
        {
            TimeoutSeconds = 60,
        };

        options.TimeoutSeconds.ShouldBe(60);
    }
}
