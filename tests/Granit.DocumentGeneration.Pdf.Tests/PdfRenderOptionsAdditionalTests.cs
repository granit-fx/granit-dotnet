using Granit.DocumentGeneration.Pdf.Options;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class PdfRenderOptionsAdditionalTests
{
    [Fact]
    public void CanSet_PaperFormat()
    {
        PdfRenderOptions options = new() { PaperFormat = "Letter" };
        options.PaperFormat.ShouldBe("Letter");
    }

    [Fact]
    public void CanSet_Landscape()
    {
        PdfRenderOptions options = new() { Landscape = true };
        options.Landscape.ShouldBeTrue();
    }

    [Fact]
    public void CanSet_AllMargins()
    {
        PdfRenderOptions options = new()
        {
            MarginTop = "20mm",
            MarginBottom = "15mm",
            MarginLeft = "25mm",
            MarginRight = "30mm",
        };

        options.MarginTop.ShouldBe("20mm");
        options.MarginBottom.ShouldBe("15mm");
        options.MarginLeft.ShouldBe("25mm");
        options.MarginRight.ShouldBe("30mm");
    }

    [Fact]
    public void CanSet_HeaderTemplate()
    {
        PdfRenderOptions options = new() { HeaderTemplate = "<div>Header</div>" };
        options.HeaderTemplate.ShouldBe("<div>Header</div>");
    }

    [Fact]
    public void CanSet_FooterTemplate()
    {
        PdfRenderOptions options = new() { FooterTemplate = "<div>Footer</div>" };
        options.FooterTemplate.ShouldBe("<div>Footer</div>");
    }

    [Fact]
    public void CanSet_PrintBackground()
    {
        PdfRenderOptions options = new() { PrintBackground = false };
        options.PrintBackground.ShouldBeFalse();
    }

}
