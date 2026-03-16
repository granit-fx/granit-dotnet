using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class ImageAnalysisTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var analysis = new ImageAnalysis(
            "A sunset over the ocean",
            ["sun", "ocean", "horizon"],
            ["nature", "landscape"],
            "Sunset over ocean with orange sky");

        analysis.Description.ShouldBe("A sunset over the ocean");
        analysis.DetectedObjects.ShouldBe(["sun", "ocean", "horizon"]);
        analysis.Tags.ShouldBe(["nature", "landscape"]);
        analysis.SuggestedAltText.ShouldBe("Sunset over ocean with orange sky");
    }

    [Fact]
    public void Constructor_NullAltText_IsAllowed()
    {
        var analysis = new ImageAnalysis("Abstract", [], [], null);

        analysis.SuggestedAltText.ShouldBeNull();
    }
}
