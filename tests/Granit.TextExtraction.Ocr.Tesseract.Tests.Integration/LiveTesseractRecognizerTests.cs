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
        // GRANIT_TESSERACT_LIB_DIR points at a root whose `x64/` subdir contains
        // `libleptonica-1.82.0.so` + `libtesseract50.so` symlinks — the layout the
        // Charlesw NuGet's InteropDotNet loader requires. CI prepares this under
        // /opt/granit-ocr-libs; local opt-in is described in the package README.
        string? libDir = Environment.GetEnvironmentVariable("GRANIT_TESSERACT_LIB_DIR");

        ServiceCollection services = [];
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddTesseractOcrExtractor(o =>
        {
            o.DataPath = TessdataPath;
            o.Language = "eng";
            if (!string.IsNullOrEmpty(libDir))
            {
                o.LibrarySearchPath = libDir;
            }
        });
        return services.BuildServiceProvider();
    }

    private static byte[] RenderPngWithText(string text)
    {
        // Magick.NET (Apache-2.0, native deps bundled in the Q8-AnyCPU package) renders
        // the caption to a PNG. We pin a font file path explicitly because the bundled
        // libfreetype probes via fontconfig — on bare Ubuntu CI images without
        // `fontconfig` + `fonts-dejavu-core` the probe returns `(null)` and IM throws
        // MagickTypeErrorException. The CI integration shard installs both deps; the
        // path resolution falls through to a known-good Linux location.
        string fontPath = ResolveFontPath();
        MagickReadSettings settings = new()
        {
            Width = 600,
            Height = 200,
            BackgroundColor = MagickColors.White,
            FillColor = MagickColors.Black,
            FontPointsize = 64,
            Font = fontPath,
        };
        using MagickImage image = new($"label:{text}", settings);
        image.BorderColor = MagickColors.White;
        image.Border(20);
        return image.ToByteArray(MagickFormat.Png);
    }

    private static string ResolveFontPath()
    {
        // Candidate list, ordered by what the CI integration shard installs first
        // (`fonts-dejavu-core`). Other entries are fallbacks for developer machines.
        string[] candidates =
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        ];
        return Array.Find(candidates, File.Exists)
            ?? throw new FileNotFoundException(
                "No usable system font (DejaVu Sans / Liberation Sans) was found. " +
                "Install fonts-dejavu-core on Debian/Ubuntu or set TESSDATA_PREFIX " +
                "only on a machine that has a known TrueType font installed.");
    }
}
