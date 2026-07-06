using Granit.Templating.AI.Options;
using Shouldly;

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

}
