using Granit.DocumentGeneration.Pdf.Internal;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Shouldly;
using Xunit;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class PuppeteerSharpRendererTests
{
    [Fact]
    public void CanRender_Pdf_ReturnsTrue()
    {
        PuppeteerSharpRenderer renderer = CreateRenderer();

        bool result = renderer.CanRender(DocumentFormat.Pdf);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(DocumentFormat.Html)]
    [InlineData(DocumentFormat.Excel)]
    public void CanRender_NonPdf_ReturnsFalse(DocumentFormat format)
    {
        PuppeteerSharpRenderer renderer = CreateRenderer();

        bool result = renderer.CanRender(format);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("A0")]
    [InlineData("A1")]
    [InlineData("A2")]
    [InlineData("A3")]
    [InlineData("A4")]
    [InlineData("A5")]
    [InlineData("A6")]
    [InlineData("Letter")]
    [InlineData("Legal")]
    [InlineData("Tabloid")]
    [InlineData("Ledger")]
    public void ResolvePaperFormat_KnownFormats_ReturnsCorrectFormat(string format)
    {
        PaperFormat result = PuppeteerSharpRenderer.ResolvePaperFormat(format);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void ResolvePaperFormat_A4_ReturnsA4() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("A4").ShouldBe(PaperFormat.A4);

    [Fact]
    public void ResolvePaperFormat_Letter_ReturnsLetter() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("Letter").ShouldBe(PaperFormat.Letter);

    [Fact]
    public void ResolvePaperFormat_CaseInsensitive() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("a4").ShouldBe(PaperFormat.A4);

    [Fact]
    public void ResolvePaperFormat_Unknown_DefaultsToA4() =>
        PuppeteerSharpRenderer.ResolvePaperFormat("B5").ShouldBe(PaperFormat.A4);

    [Fact]
    public async Task RenderAsync_WhenBrowserNotStarted_ThrowsInvalidOperationException()
    {
        PuppeteerSharpRenderer renderer = CreateRenderer();

        Func<Task> act = () => renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("Chromium browser is not available");
    }

    [Fact]
    public async Task RenderAsync_WithMockedBrowser_ReturnsPdfResult()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46]; // %PDF magic bytes
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

        DocumentResult result = await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        result.Format.ShouldBe(DocumentFormat.Pdf);
        result.Content.ToArray().ShouldBe(fakePdf);
    }

    [Fact]
    public async Task RenderAsync_WithHeaderFooter_SetsDisplayHeaderFooter()
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
            HeaderTemplate = "<div>Header</div>",
            FooterTemplate = "<div>Footer</div>",
        };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o =>
            o.DisplayHeaderFooter == true &&
            o.HeaderTemplate == "<div>Header</div>" &&
            o.FooterTemplate == "<div>Footer</div>"));
    }

    [Fact]
    public async Task RenderAsync_WithLandscape_SetsLandscapeOption()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>()).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new() { Landscape = true };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o => o.Landscape == true));
    }

    [Fact]
    public async Task RenderAsync_WithCustomMargins_SetsMarginOptions()
    {
        byte[] fakePdf = [0x25, 0x50, 0x44, 0x46];
        PdfOptions? capturedOptions = null;
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Do<PdfOptions>(o => capturedOptions = o)).Returns(fakePdf);

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new()
        {
            MarginTop = "20mm",
            MarginBottom = "15mm",
            MarginLeft = "25mm",
            MarginRight = "25mm",
        };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        capturedOptions.ShouldNotBeNull();
        capturedOptions!.MarginOptions.Top.ShouldBe("20mm");
        capturedOptions.MarginOptions.Bottom.ShouldBe("15mm");
        capturedOptions.MarginOptions.Left.ShouldBe("25mm");
        capturedOptions.MarginOptions.Right.ShouldBe("25mm");
    }

    [Fact]
    public async Task RenderAsync_SetsContentWithDomContentLoaded()
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

        await renderer.RenderAsync("<h1>Hello</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).SetContentAsync(
            "<h1>Hello</h1>",
            Arg.Is<NavigationOptions>(o =>
                o.WaitUntil != null &&
                o.WaitUntil.Length == 1 &&
                o.WaitUntil[0] == WaitUntilNavigation.DOMContentLoaded));
    }

    [Fact]
    public async Task RenderAsync_WithOnlyHeaderTemplate_SetsEmptyFooter()
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
            HeaderTemplate = "<div>Header Only</div>",
        };
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        await renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await page.Received(1).PdfDataAsync(Arg.Is<PdfOptions>(o =>
            o.DisplayHeaderFooter == true &&
            o.HeaderTemplate == "<div>Header Only</div>" &&
            o.FooterTemplate!.Contains("pageNumber")));
    }

    [Fact]
    public async Task RenderAsync_ReleasesSemaphore_EvenOnException()
    {
        IPage page = Substitute.For<IPage>();
        page.SetContentAsync(Arg.Any<string>(), Arg.Any<NavigationOptions>())
            .Returns(Task.CompletedTask);
        page.PdfDataAsync(Arg.Any<PdfOptions>())
            .ThrowsAsync(new PuppeteerException("Chromium crashed"));

        IBrowser browser = Substitute.For<IBrowser>();
        browser.NewPageAsync().Returns(page);

        PdfRenderOptions opts = new();
        ChromiumLifetimeService lifetime = CreateLifetimeWithBrowser(browser, opts);
        PuppeteerSharpRenderer renderer = new(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<PuppeteerSharpRenderer>.Instance);

        Func<Task> act = () => renderer.RenderAsync("<h1>Test</h1>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<PuppeteerException>(act);

        // Semaphore should be released — verify by checking current count
        lifetime.PageSemaphore.CurrentCount.ShouldBe(opts.MaxConcurrentPages);
    }

    private static PuppeteerSharpRenderer CreateRenderer()
    {
        PdfRenderOptions options = new();
        ChromiumLifetimeService lifetime = new(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ChromiumLifetimeService>.Instance);

        return new PuppeteerSharpRenderer(
            lifetime,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<PuppeteerSharpRenderer>.Instance);
    }

    private static ChromiumLifetimeService CreateLifetimeWithBrowser(IBrowser browser, PdfRenderOptions options)
    {
        ChromiumLifetimeService lifetime = new(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ChromiumLifetimeService>.Instance);

        // Use reflection to set the browser field for testing
        System.Reflection.FieldInfo? browserField = typeof(ChromiumLifetimeService)
            .GetField("_browser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        browserField!.SetValue(lifetime, browser);

        return lifetime;
    }
}
