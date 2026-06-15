using Granit.TextExtraction.Pdf.Ocr.Options;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Pdf.Ocr.Tests;

public sealed class PdfOcrOptionsTests
{
    [Fact]
    public void Section_name_matches_convention() =>
        PdfOcrOptions.SectionName.ShouldBe("TextExtraction:Pdf:Ocr");
}
