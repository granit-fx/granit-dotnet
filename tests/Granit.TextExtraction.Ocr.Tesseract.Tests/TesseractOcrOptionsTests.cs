using Granit.TextExtraction.Ocr.Tesseract.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.Tesseract.Tests;

public sealed class TesseractOcrOptionsTests
{
    [Fact]
    public void Section_name_matches_convention() =>
        TesseractOcrOptions.SectionName.ShouldBe("TextExtraction:Ocr:Tesseract");
}
