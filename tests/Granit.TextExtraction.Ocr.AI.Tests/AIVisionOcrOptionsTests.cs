using Granit.TextExtraction.Ocr.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.AI.Tests;

public sealed class AIVisionOcrOptionsTests
{
    [Fact]
    public void Section_name_matches_convention() =>
        AIVisionOcrOptions.SectionName.ShouldBe("TextExtraction:Ocr:AI");
}
