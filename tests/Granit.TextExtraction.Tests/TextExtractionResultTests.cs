using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tests;

public sealed class TextExtractionResultTests
{
    [Fact]
    public void Carries_all_metadata_from_constructor()
    {
        TextExtractionResult result = new(
            Content: "hello",
            DetectedLanguage: "en",
            IsTruncated: true,
            CharCount: 5,
            ExtractorName: "granit.test");

        result.Content.ShouldBe("hello");
        result.DetectedLanguage.ShouldBe("en");
        result.IsTruncated.ShouldBeTrue();
        result.CharCount.ShouldBe(5);
        result.ExtractorName.ShouldBe("granit.test");
    }

    [Fact]
    public void Is_a_value_record()
    {
        TextExtractionResult a = new("x", null, false, 1, "n");
        TextExtractionResult b = new("x", null, false, 1, "n");

        a.ShouldBe(b);
    }
}
