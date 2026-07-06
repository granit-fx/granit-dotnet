using Granit.Localization.AI.Options;
using Shouldly;

namespace Granit.Localization.AI.Tests;

public sealed class LocalizationAIOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => LocalizationAIOptions.SectionName.ShouldBe("Localization:AI");

    [Fact]
    public void WorkspaceName_DefaultsToDefault()
    {
        LocalizationAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void TimeoutSeconds_DefaultsTo30()
    {
        LocalizationAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(30);
    }
}
