using Granit.DocumentGeneration.Pdf.Internal;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class PuppeteerSharpRendererAdditionalTests
{
    // -------------------------------------------------------------------------
    // ResolvePaperFormat — additional format coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolvePaperFormat_A0_ReturnsA0() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A0").ShouldBe(PaperFormat.A0);

    [Fact]
    public void ResolvePaperFormat_A1_ReturnsA1() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A1").ShouldBe(PaperFormat.A1);

    [Fact]
    public void ResolvePaperFormat_A2_ReturnsA2() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A2").ShouldBe(PaperFormat.A2);

    [Fact]
    public void ResolvePaperFormat_A3_ReturnsA3() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A3").ShouldBe(PaperFormat.A3);

    [Fact]
    public void ResolvePaperFormat_A5_ReturnsA5() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A5").ShouldBe(PaperFormat.A5);

    [Fact]
    public void ResolvePaperFormat_A6_ReturnsA6() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A6").ShouldBe(PaperFormat.A6);

    [Fact]
    public void ResolvePaperFormat_Legal_ReturnsLegal() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("Legal").ShouldBe(PaperFormat.Legal);

    [Fact]
    public void ResolvePaperFormat_Tabloid_ReturnsTabloid() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("Tabloid").ShouldBe(PaperFormat.Tabloid);

    [Fact]
    public void ResolvePaperFormat_Ledger_ReturnsLedger() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("Ledger").ShouldBe(PaperFormat.Ledger);

    [Theory]
    [InlineData("letter")]
    [InlineData("LETTER")]
    [InlineData("Letter")]
    public void ResolvePaperFormat_CaseInsensitive_AllVariants(string format) =>
        PuppeteerSharpRenderer.ResolvePaperFormat(format).ShouldBe(PaperFormat.Letter);

    // -------------------------------------------------------------------------
    // RenderAsync — footer-only template
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_WithOnlyFooterTemplate_SetsEmptyHeader()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new()
        {
            FooterTemplate = "<div>Page Footer</div>",
        };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o =>
            o.DisplayHeaderFooter == true &&
            o.HeaderTemplate == "<span></span>" &&
            o.FooterTemplate == "<div>Page Footer</div>"));
    }

    // -------------------------------------------------------------------------
    // RenderAsync — no header/footer
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_WithoutHeaderFooter_DoesNotDisplayHeaderFooter()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new();
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o =>
            o.DisplayHeaderFooter == false));
    }

    // -------------------------------------------------------------------------
    // RenderAsync — print background
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_PrintBackgroundTrue_SetsPrintBackgroundOption()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new() { PrintBackground = true };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o => o.PrintBackground == true));
    }

    // -------------------------------------------------------------------------
    // RenderAsync — paper format resolved correctly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RenderAsync_WithLetterFormat_ResolvesCorrectPaperFormat()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new() { PaperFormat = "Letter" };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o =>
            o.Format == PaperFormat.Letter));
    }

    private static ChromiumLifetimeService CreateLifetimeWithBrowser(IBrowser browser, PdfRenderOptions options)
    {
        ChromiumLifetimeService lifetime = new(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ChromiumLifetimeService>.Instance);

        System.Reflection.FieldInfo? browserField = typeof(ChromiumLifetimeService)
            .GetField("_browser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        browserField!.SetValue(lifetime, browser);

        return lifetime;
    }
}
