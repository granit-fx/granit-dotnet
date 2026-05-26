using Granit.TextExtraction.Ocr.Tesseract.Extensions;
using ImageMagick;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Ocr.Tesseract.Tests.Integration;

/// <summary>
/// End-to-end OCR round-trip against the real native libtesseract. The unit-test suite
/// in Granit.TextExtraction.Ocr.Tesseract.Tests mocks <see cref="ITesseractRecognizer"/>;
/// without these tests, <c>DefaultTesseractRecognizer</c> is exercised in zero pipelines
/// — Pix loader / traineddata-path / locale-mismatch regressions would slip through.
/// </summary>
/// <remarks>
/// Opt-in: set <c>TESSDATA_PREFIX</c> to the directory containing
/// <c>eng.traineddata</c> (typically <c>/usr/share/tesseract-ocr/5/tessdata</c> on
/// Debian/Ubuntu). Without it the suite skips so developer machines without
/// libtesseract still run the unit suite.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class LiveTesseractRecognizerTests
{
    private static readonly string? TessdataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");

    public static bool LibTesseractAvailable => !string.IsNullOrWhiteSpace(TessdataPath);

    [Fact(SkipUnless = nameof(LibTesseractAvailable),
        Skip = "TESSDATA_PREFIX is not set; libtesseract integration tests are opt-in. Install libtesseract + a traineddata package and export TESSDATA_PREFIX to enable.")]
    public async Task RecognizeAsync_decodes_rendered_text_through_the_default_recognizer()
    {
        byte[] png = RenderPngWithText("GRANIT OCR");

        await using ServiceProvider sp = BuildHost();
        ITesseractRecognizer recognizer = sp.GetRequiredService<ITesseractRecognizer>();

        string text = await recognizer.RecognizeAsync(png, TestContext.Current.CancellationToken);

        // Tesseract often emits trailing whitespace / newline characters; assert on the
        // payload only. The first character can also be flaky on some font/render
        // combos so we settle for a stable substring.
        text.ShouldContain("GRANIT", Case.Insensitive);
    }

    [Fact(SkipUnless = nameof(LibTesseractAvailable),
        Skip = "TESSDATA_PREFIX is not set; libtesseract integration tests are opt-in. Install libtesseract + a traineddata package and export TESSDATA_PREFIX to enable.")]
    public async Task TesseractOcrExtractor_round_trip_returns_recognised_text()
    {
        byte[] png = RenderPngWithText("HELLO OCR");

        await using ServiceProvider sp = BuildHost();
        TesseractOcrExtractor extractor = sp.GetServices<ITextExtractor>()
            .OfType<TesseractOcrExtractor>()
            .Single();

        using MemoryStream stream = new(png);
        TextExtractionResult result = await extractor.ExtractAsync(
            stream,
            contentType: "image/png",
            maxCharLength: 4096,
            TestContext.Current.CancellationToken);

        result.Content.ShouldNotBeNullOrWhiteSpace();
        result.Content.ShouldContain("HELLO", Case.Insensitive);
        result.ExtractorName.ShouldBe(TesseractOcrExtractor.ExtractorName);
    }

    private static ServiceProvider BuildHost()
    {
        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddTesseractOcrExtractor(o =>
        {
            o.DataPath = TessdataPath;
            o.Language = "eng";
        });
        return services.BuildServiceProvider();
    }

    private static byte[] RenderPngWithText(string text)
    {
        // Magick.NET (Apache-2.0, native deps bundled in the Q8-AnyCPU package) renders
        // the caption to a PNG without requiring a system-font lookup — IM picks a built-in
        // font, sized to fit. The output resolution is intentionally generous so Tesseract
        // has clean glyph edges to work with.
        MagickReadSettings settings = new()
        {
            Width = 600,
            Height = 200,
            BackgroundColor = MagickColors.White,
            FillColor = MagickColors.Black,
            FontPointsize = 64,
        };
        using MagickImage image = new($"label:{text}", settings);
        image.BorderColor = MagickColors.White;
        image.Border(20);
        return image.ToByteArray(MagickFormat.Png);
    }
}
