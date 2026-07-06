using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ImageAnalysisTests
{
    [Fact]
    public void Constructor_NullAltText_IsAllowed()
    {
        var analysis = new ImageAnalysis("Abstract", [], [], null);

        analysis.SuggestedAltText.ShouldBeNull();
    }
}
