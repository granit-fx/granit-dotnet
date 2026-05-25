using Granit.TextExtraction.Ocr.Tesseract.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.TextExtraction.Ocr.Tesseract.Tests;

public sealed class TesseractOcrExtractorTests
{
    private const string Png = "image/png";

    private static (TesseractOcrExtractor extractor, ITesseractRecognizer recognizer) CreateExtractor(
        string recognizedText = "recognised text",
        ExtractionOptions? extractionOptions = null,
        TesseractOcrOptions? ocrOptions = null)
    {
        ITesseractRecognizer recognizer = Substitute.For<ITesseractRecognizer>();
        recognizer.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(recognizedText));

        TesseractOcrExtractor extractor = new(
            recognizer,
            MEOptions.Create(extractionOptions ?? new ExtractionOptions()),
            MEOptions.Create(ocrOptions ?? new TesseractOcrOptions { DataPath = "/tmp/tessdata" }),
            NullLogger<TesseractOcrExtractor>.Instance);

        return (extractor, recognizer);
    }

    private static byte[] PngBytes(int width, int height)
    {
        using Image<Rgba32> img = new(width, height);
        using MemoryStream ms = new();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/tiff", true)]
    [InlineData("image/bmp", true)]
    [InlineData("image/webp", false)] // webp is not in the Tesseract allowlist (handled by Ocr.AI)
    [InlineData("application/pdf", false)] // scanned PDF deferred to follow-up
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_configured_image_types(string contentType, bool expected)
    {
        (TesseractOcrExtractor extractor, _) = CreateExtractor();
        extractor.CanHandle(contentType).ShouldBe(expected);
    }

    [Fact]
    public async Task Happy_path_calls_recognizer_and_returns_text()
    {
        (TesseractOcrExtractor extractor, ITesseractRecognizer recognizer) =
            CreateExtractor("Hello, OCR!");

        byte[] imageBytes = PngBytes(32, 32);
        using MemoryStream input = new(imageBytes);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBe("Hello, OCR!");
        result.ExtractorName.ShouldBe(TesseractOcrExtractor.ExtractorName);
        result.IsTruncated.ShouldBeFalse();

        await recognizer.Received(1).RecognizeAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(imageBytes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Truncates_when_recognition_exceeds_max_char_length()
    {
        (TesseractOcrExtractor extractor, _) = CreateExtractor(new string('x', 5_000));
        using MemoryStream input = new(PngBytes(16, 16));

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 100,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsTruncated.ShouldBeTrue();
        result.Content.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Pixel_bomb_is_rejected_before_calling_recognizer()
    {
        // 32×32 image = 1024 pixels, cap at 4 → exceeds, soft-skip.
        TesseractOcrOptions tight = new()
        {
            DataPath = "/tmp/tessdata",
            MaxImagePixels = 4,
        };
        (TesseractOcrExtractor extractor, ITesseractRecognizer recognizer) =
            CreateExtractor(ocrOptions: tight);

        using MemoryStream input = new(PngBytes(32, 32));

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
        result.ExtractorName.ShouldBe(TesseractOcrExtractor.ExtractorName);

        await recognizer.DidNotReceive().RecognizeAsync(
            Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Malformed_image_bytes_soft_skip()
    {
        (TesseractOcrExtractor extractor, ITesseractRecognizer recognizer) = CreateExtractor();

        // Random bytes — no PNG/JPEG/etc header → ImageSharp throws UnknownImageFormatException.
        byte[] junk = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09];
        using MemoryStream input = new(junk);

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
        await recognizer.DidNotReceive().RecognizeAsync(
            Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recognizer_failure_returns_skipped_result()
    {
        ITesseractRecognizer recognizer = Substitute.For<ITesseractRecognizer>();
        recognizer.RecognizeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("traineddata not found"));

        TesseractOcrExtractor extractor = new(
            recognizer,
            MEOptions.Create(new ExtractionOptions()),
            MEOptions.Create(new TesseractOcrOptions { DataPath = "/tmp/tessdata" }),
            NullLogger<TesseractOcrExtractor>.Instance);

        using MemoryStream input = new(PngBytes(16, 16));

        TextExtractionResult result = await extractor.ExtractAsync(
            input, Png, maxCharLength: 1024,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Content.ShouldBeEmpty();
        result.IsTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Body_size_cap_throws_input_too_large()
    {
        ExtractionOptions extraction = new() { MaxBodySizeBytes = 16 };
        (TesseractOcrExtractor extractor, _) = CreateExtractor(extractionOptions: extraction);

        // A real PNG even of 1×1 is ~70 bytes — exceeds the 16-byte cap easily.
        using MemoryStream input = new(PngBytes(8, 8));

        TextExtraction.Exceptions.TextExtractionException tex =
            await Should.ThrowAsync<TextExtraction.Exceptions.TextExtractionException>(
                async () => await extractor.ExtractAsync(
                    input, Png, maxCharLength: 1024,
                    cancellationToken: TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        ITesseractRecognizer recognizer = Substitute.For<ITesseractRecognizer>();
        Microsoft.Extensions.Options.IOptions<ExtractionOptions> extraction =
            MEOptions.Create(new ExtractionOptions());
        Microsoft.Extensions.Options.IOptions<TesseractOcrOptions> ocr =
            MEOptions.Create(new TesseractOcrOptions { DataPath = "/tmp/tessdata" });
        NullLogger<TesseractOcrExtractor> logger = NullLogger<TesseractOcrExtractor>.Instance;

        Should.Throw<ArgumentNullException>(() => new TesseractOcrExtractor(null!, extraction, ocr, logger));
        Should.Throw<ArgumentNullException>(() => new TesseractOcrExtractor(recognizer, null!, ocr, logger));
        Should.Throw<ArgumentNullException>(() => new TesseractOcrExtractor(recognizer, extraction, null!, logger));
        Should.Throw<ArgumentNullException>(() => new TesseractOcrExtractor(recognizer, extraction, ocr, null!));
    }
}
