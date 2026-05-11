using Granit.DocumentGeneration.Pdf.Options;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class PdfRenderOptionsTests
{
    [Fact]
    public void SectionName_IsDocumentGenerationPdf() =>
        PdfRenderOptions.SectionName.ShouldBe("DocumentGeneration:Pdf");

    [Fact]
    public void Defaults_PaperFormat_IsA4()
    {
        PdfRenderOptions options = new();
        options.PaperFormat.ShouldBe("A4");
    }

    [Fact]
    public void Defaults_Landscape_IsFalse()
    {
        PdfRenderOptions options = new();
        options.Landscape.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_PrintBackground_IsTrue()
    {
        PdfRenderOptions options = new();
        options.PrintBackground.ShouldBeTrue();
    }

    [Fact]
    public void Defaults_Margins_Are10mm()
    {
        PdfRenderOptions options = new();
        options.MarginTop.ShouldBe("10mm");
        options.MarginBottom.ShouldBe("10mm");
        options.MarginLeft.ShouldBe("10mm");
        options.MarginRight.ShouldBe("10mm");
    }

    [Fact]
    public void Defaults_HeaderIsNull_FooterHasPageNumbers()
    {
        PdfRenderOptions options = new();
        options.HeaderTemplate.ShouldBeNull();
        options.FooterTemplate.ShouldNotBeNullOrEmpty();
        options.FooterTemplate.ShouldContain("pageNumber");
    }
}
