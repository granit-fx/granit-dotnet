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
            ExtractorName: "granit.test",
            Confidence: ExtractionConfidence.Heuristic);

        result.Content.ShouldBe("hello");
        result.DetectedLanguage.ShouldBe("en");
        result.IsTruncated.ShouldBeTrue();
        result.CharCount.ShouldBe(5);
        result.ExtractorName.ShouldBe("granit.test");
        result.Confidence.ShouldBe(ExtractionConfidence.Heuristic);
    }

    [Fact]
    public void Is_a_value_record()
    {
        TextExtractionResult a = new("x", null, false, 1, "n", ExtractionConfidence.Deterministic);
        TextExtractionResult b = new("x", null, false, 1, "n", ExtractionConfidence.Deterministic);

        a.ShouldBe(b);
    }

    [Fact]
    public void Two_results_with_different_confidence_are_not_equal()
    {
        TextExtractionResult a = new("x", null, false, 1, "n", ExtractionConfidence.Deterministic);
        TextExtractionResult b = new("x", null, false, 1, "n", ExtractionConfidence.ModelGenerated);

        a.ShouldNotBe(b);
    }
}
