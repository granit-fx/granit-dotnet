using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Options;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Pdf.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Pdf.Tests;

public sealed class PdfRenditionProviderTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("image/png", false)]
    [InlineData("", false)]
    public void CanHandle_only_recognises_pdf(string contentType, bool expected) =>
        new PdfRenditionProvider(
            Substitute.For<IHeadlessBrowser>(),
            Substitute.For<IPdfViewerCapability>())
            .CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public void OutputContentType_is_png() =>
        new PdfRenditionProvider(
            Substitute.For<IHeadlessBrowser>(),
            Substitute.For<IPdfViewerCapability>())
            .OutputContentType.ShouldBe("image/png");

    [Fact]
    public async Task GenerateAsync_renders_first_page_at_target_dimensions()
    {
        IBrowserPage page = Substitute.For<IBrowserPage>();
        IHeadlessBrowser browser = Substitute.For<IHeadlessBrowser>();
        browser.AcquirePageAsync(Arg.Any<BrowserPageOptions?>(), Arg.Any<CancellationToken>())
            .Returns(page);

        IPdfDocumentPage pdf = Substitute.For<IPdfDocumentPage>();
        pdf.RenderPageToImageAsync(
                0,
                Arg.Any<Granit.Browsing.Capabilities.RenditionDimensions>(),
                Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        IPdfViewerCapability viewer = Substitute.For<IPdfViewerCapability>();
        viewer.OpenPdfAsync(page, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(pdf);

        var provider = new PdfRenditionProvider(browser, viewer);
        using MemoryStream src = new(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var target = new RenditionTarget(
            RenditionType.Thumbnail, "image/png", new Granit.Documents.Renditions.RenditionDimensions(400, 600));

        RenditionResult result = await provider.GenerateAsync(
            src, "application/pdf", target, CancellationToken.None);

        result.ContentType.ShouldBe("image/png");
        result.Content.Length.ShouldBe(4);
        result.Width.ShouldBe(400);
        result.Height.ShouldBe(600);

        await pdf.Received(1).RenderPageToImageAsync(
            0,
            Arg.Is<Granit.Browsing.Capabilities.RenditionDimensions>(d => d.Width == 400 && d.Height == 600),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_falls_back_to_default_dimensions_when_target_has_none()
    {
        IBrowserPage page = Substitute.For<IBrowserPage>();
        IHeadlessBrowser browser = Substitute.For<IHeadlessBrowser>();
        browser.AcquirePageAsync(Arg.Any<BrowserPageOptions?>(), Arg.Any<CancellationToken>())
            .Returns(page);

        IPdfDocumentPage pdf = Substitute.For<IPdfDocumentPage>();
        pdf.RenderPageToImageAsync(
                Arg.Any<int>(),
                Arg.Any<Granit.Browsing.Capabilities.RenditionDimensions>(),
                Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        IPdfViewerCapability viewer = Substitute.For<IPdfViewerCapability>();
        viewer.OpenPdfAsync(page, Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(pdf);

        var provider = new PdfRenditionProvider(browser, viewer);
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Thumbnail, "image/png");

        RenditionResult result = await provider.GenerateAsync(
            src, "application/pdf", target, CancellationToken.None);

        result.Width.ShouldBe(800);
        result.Height.ShouldBe(1132);
    }
}
