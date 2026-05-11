using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Pages;
using Granit.DocumentGeneration.Pdf.Internal;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using BrowsingPaperFormat = Granit.Browsing.Capabilities.PaperFormat;
using BrowsingPdfOptions = Granit.Browsing.Capabilities.PdfOptions;

namespace Granit.DocumentGeneration.Pdf.Tests;

public sealed class BrowsingPdfRendererTests
{
    [Fact]
    public void CanRender_only_returns_true_for_Pdf()
    {
        BrowsingPdfRenderer renderer = BuildRenderer(out _, out _);

        renderer.CanRender(DocumentFormat.Pdf).ShouldBeTrue();
        renderer.CanRender(DocumentFormat.Html).ShouldBeFalse();
        renderer.CanRender(DocumentFormat.Excel).ShouldBeFalse();
    }

    [Fact]
    public async Task RenderAsync_acquires_page_and_invokes_pdf_capability()
    {
        BrowsingPdfRenderer renderer = BuildRenderer(out IHeadlessBrowser browser, out IPdfCapability pdf);
        IBrowserPage page = Substitute.For<IBrowserPage>();
        browser.AcquirePageAsync(Arg.Any<Granit.Browsing.Options.BrowserPageOptions?>(), Arg.Any<CancellationToken>())
            .Returns(page);
        pdf.RenderToPdfAsync(page, Arg.Any<BrowsingPdfOptions>(), Arg.Any<CancellationToken>())
            .Returns([0x25, 0x50, 0x44, 0x46]); // %PDF prefix

        DocumentResult result = await renderer.RenderAsync("<p>hi</p>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        result.Format.ShouldBe(DocumentFormat.Pdf);
        result.Content.Length.ShouldBe(4);
        await page.Received(1).SetContentAsync("<p>hi</p>", Arg.Any<Granit.Browsing.Options.NavigationOptions?>(), Arg.Any<CancellationToken>());
        await pdf.Received(1).RenderToPdfAsync(page, Arg.Any<BrowsingPdfOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_disables_javascript_and_blocks_network()
    {
        BrowsingPdfRenderer renderer = BuildRenderer(out IHeadlessBrowser browser, out IPdfCapability pdf);
        IBrowserPage page = Substitute.For<IBrowserPage>();
        browser.AcquirePageAsync(Arg.Any<Granit.Browsing.Options.BrowserPageOptions?>(), Arg.Any<CancellationToken>())
            .Returns(page);
        pdf.RenderToPdfAsync(page, Arg.Any<BrowsingPdfOptions>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await renderer.RenderAsync("<p>hi</p>", DocumentFormat.Pdf, TestContext.Current.CancellationToken);

        await browser.Received(1).AcquirePageAsync(
            Arg.Is<Granit.Browsing.Options.BrowserPageOptions?>(o => o != null && !o.JavaScriptEnabled),
            Arg.Any<CancellationToken>());
        await page.Received(1).RouteAsync(
            Arg.Any<RoutePattern>(),
            Arg.Any<Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>>>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("A4", BrowsingPaperFormat.A4)]
    [InlineData("a4", BrowsingPaperFormat.A4)]
    [InlineData("A3", BrowsingPaperFormat.A3)]
    [InlineData("Letter", BrowsingPaperFormat.Letter)]
    [InlineData("LEGAL", BrowsingPaperFormat.Legal)]
    [InlineData("Tabloid", BrowsingPaperFormat.Tabloid)]
    [InlineData("Unknown", BrowsingPaperFormat.A4)]
    public void ResolvePaperFormat_maps_known_strings_and_falls_back_to_A4(string input, BrowsingPaperFormat expected) =>
        BrowsingPdfRenderer.ResolvePaperFormat(input).ShouldBe(expected);

    private static BrowsingPdfRenderer BuildRenderer(out IHeadlessBrowser browser, out IPdfCapability pdf)
    {
        browser = Substitute.For<IHeadlessBrowser>();
        pdf = Substitute.For<IPdfCapability>();
        return new BrowsingPdfRenderer(
            browser,
            pdf,
            Microsoft.Extensions.Options.Options.Create(new PdfRenderOptions()),
            NullLogger<BrowsingPdfRenderer>.Instance);
    }
}
