using Granit.TextExtraction.Pdf.Ocr.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Pdf.Ocr.Tests;

public sealed class PdfOcrTextExtractorTests
{
    private const string Pdf = "application/pdf";
    private const string ImagePng = "image/png";

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("image/png", false)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_returns_expected_value(string contentType, bool expected) =>
        BuildExtractor().extractor.CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task Text_only_pdf_does_not_call_the_rasterizer()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor();

        byte[] pdf = PdfFixtures.TextPage("hello native text extraction this should easily exceed the threshold");
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldContain("hello");
        result.Confidence.ShouldBe(ExtractionConfidence.Deterministic);
        await rasterizer.DidNotReceiveWithAnyArgs().RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await ocr.DidNotReceiveWithAnyArgs().ExtractAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scanned_pdf_routes_through_the_ocr_extractor_and_returns_its_text()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor();

        // ReturnsForAnyArgs / ReceivedWithAnyArgs sidestep NSubstitute's matcher queue
        // edge cases when chaining Arg.Any<T>() across value-typed parameters.
        rasterizer.RasterisePageAsync(
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new byte[] { 0xAB, 0xCD });
        ocr.CanHandle(ImagePng).Returns(true);
        ocr.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new TextExtractionResult(
                Content: "recognised body via OCR",
                DetectedLanguage: null,
                IsTruncated: false,
                CharCount: 23,
                ExtractorName: "fake.ocr",
                Confidence: ExtractionConfidence.Heuristic));

        byte[] pdf = PdfFixtures.EmptyPage();
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldContain("recognised body via OCR");
        result.Confidence.ShouldBe(ExtractionConfidence.Heuristic,
            "any OCR'd page drops the overall confidence to Heuristic.");
        await rasterizer.ReceivedWithAnyArgs(1).RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mixed_pdf_preserves_page_order_native_then_ocr()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor();

        rasterizer.RasterisePageAsync(
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new byte[] { 0xAB, 0xCD });
        ocr.CanHandle(ImagePng).Returns(true);
        ocr.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new TextExtractionResult(
                Content: "OCR_BODY",
                DetectedLanguage: null,
                IsTruncated: false,
                CharCount: 8,
                ExtractorName: "fake.ocr",
                Confidence: ExtractionConfidence.Heuristic));

        byte[] pdf = PdfFixtures.MixedTextThenScanned(
            "page one with enough native text to clear the threshold here");
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        int nativeIdx = result.Content.IndexOf("page one", StringComparison.Ordinal);
        int ocrIdx = result.Content.IndexOf("OCR_BODY", StringComparison.Ordinal);
        nativeIdx.ShouldBeGreaterThanOrEqualTo(0);
        ocrIdx.ShouldBeGreaterThan(nativeIdx, "OCR'd page must come AFTER the native-text page.");
        // Page 1 is native-text and stays out of the rasteriser; page 2 (zero-indexed: 1)
        // is the one that gets rendered. Assert via captured argument.
        await rasterizer.ReceivedWithAnyArgs(1).RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        int observedPageIndex = (int)rasterizer.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IPdfRasterizer.RasterisePageAsync))
            .GetArguments()[1]!;
        observedPageIndex.ShouldBe(1);
    }

    [Fact]
    public async Task Scanned_pdf_without_ocr_extractor_falls_through_to_empty_content()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, _) = BuildExtractor(includeOcr: false);

        byte[] pdf = PdfFixtures.EmptyPage();
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty(
            "no image/png extractor registered → scanned page is silently dropped (logged).");
        await rasterizer.DidNotReceiveWithAnyArgs().RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pixel_cap_skips_the_page_without_calling_the_rasterizer()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor(
            ocrOptions: new PdfOcrOptions { MaxPagePixels = 1, MinNativeCharsPerPage = 32 });

        ocr.CanHandle(ImagePng).Returns(true);

        byte[] pdf = PdfFixtures.EmptyPage();
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        await rasterizer.DidNotReceiveWithAnyArgs().RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await ocr.DidNotReceiveWithAnyArgs().ExtractAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MaxPagesToRasterise_clamps_long_documents_and_flags_truncated()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor(
            ocrOptions: new PdfOcrOptions
            {
                MinNativeCharsPerPage = 32,
                MaxPagesToRasterise = 2,
            });

        rasterizer.RasterisePageAsync(
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new byte[] { 0xAB });
        ocr.CanHandle(ImagePng).Returns(true);
        ocr.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new TextExtractionResult("page", null, false, 4, "fake.ocr", ExtractionConfidence.Heuristic));

        byte[] pdf = PdfFixtures.EmptyPages(5);
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue("3 of the 5 pages were dropped — result must signal truncation.");
        await rasterizer.ReceivedWithAnyArgs(2).RasterisePageAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Malformed_pdf_is_soft_skipped()
    {
        (PdfOcrTextExtractor extractor, _, _) = BuildExtractor();

        await using MemoryStream src = new(PdfFixtures.Malformed());

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
        result.ExtractorName.ShouldBe(PdfOcrTextExtractor.ExtractorName);
    }

    [Fact]
    public async Task Rasterizer_failure_soft_skips_the_page_and_keeps_processing()
    {
        (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) = BuildExtractor();

        rasterizer.RasterisePageAsync(
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs<byte[]>(_ => throw new InvalidOperationException("simulated PDFium failure"));
        ocr.CanHandle(ImagePng).Returns(true);

        byte[] pdf = PdfFixtures.EmptyPage();
        await using MemoryStream src = new(pdf);

        TextExtractionResult result = await extractor.ExtractAsync(src, Pdf, 4096, TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty(
            "the page rasterisation threw — page is soft-skipped and the document survives.");
        await ocr.DidNotReceiveWithAnyArgs().ExtractAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static (PdfOcrTextExtractor extractor, IPdfRasterizer rasterizer, ITextExtractor ocr) BuildExtractor(
        PdfOcrOptions? ocrOptions = null,
        ExtractionOptions? extractionOptions = null,
        bool includeOcr = true)
    {
        IPdfRasterizer rasterizer = Substitute.For<IPdfRasterizer>();
        ITextExtractor ocr = Substitute.For<ITextExtractor>();

        ServiceCollection services = [];
        if (includeOcr)
        {
            services.AddSingleton(ocr);
        }
        ServiceProvider sp = services.BuildServiceProvider();

        PdfOcrTextExtractor extractor = new(
            rasterizer,
            sp,
            MEOptions.Create(extractionOptions ?? new ExtractionOptions()),
            MEOptions.Create(ocrOptions ?? new PdfOcrOptions()),
            NullLogger<PdfOcrTextExtractor>.Instance);

        return (extractor, rasterizer, ocr);
    }
}
