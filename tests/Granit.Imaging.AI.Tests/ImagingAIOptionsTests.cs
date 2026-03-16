using Granit.Imaging.AI.Options;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ImagingAIOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new ImagingAIOptions();

        options.WorkspaceName.ShouldBeNull();
        options.TimeoutSeconds.ShouldBe(15);
    }

    [Fact]
    public void SectionName_IsCorrect() =>
        ImagingAIOptions.SectionName.ShouldBe("AI:Imaging");
}
