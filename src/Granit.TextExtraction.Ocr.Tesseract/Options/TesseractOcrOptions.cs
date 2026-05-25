namespace Granit.TextExtraction.Ocr.Tesseract.Options;

/// <summary>
/// Configuration options for the Tesseract OCR extractor. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class TesseractOcrOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction:OcrTesseract";

    /// <summary>
    /// Default pixel-bomb cap (100 megapixels — covers A1 @ 600 DPI with margin).
    /// A 100k×100k attacker-controlled image would allocate ~40 GB on the LOH if decoded;
    /// the cap is enforced BEFORE decode by reading the format header only.
    /// </summary>
    public const long DefaultMaxImagePixels = 100L * 1024 * 1024;

    /// <summary>
    /// Filesystem path to the directory containing <c>*.traineddata</c> files
    /// (e.g. <c>eng.traineddata</c>, <c>fra.traineddata</c>). On Linux the
    /// <c>tesseract-ocr-{lang}</c> system packages place these under
    /// <c>/usr/share/tesseract-ocr/{major}/tessdata/</c>; on Windows the
    /// <c>Tesseract</c> NuGet ships a default tessdata folder. There is no
    /// safe default — the host MUST set this.
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>
    /// Tesseract language code(s) — single (<c>"eng"</c>) or '+'-joined for multi-language
    /// recognition (<c>"eng+fra"</c>). The corresponding <c>*.traineddata</c> files must
    /// exist under <see cref="DataPath"/>. Defaults to English.
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Maximum decoded image surface (width × height) the extractor will accept. Images
    /// whose header reports more pixels than this are soft-skipped (no decode, no
    /// allocation). Defaults to <see cref="DefaultMaxImagePixels"/>.
    /// </summary>
    public long MaxImagePixels { get; set; } = DefaultMaxImagePixels;

    /// <summary>
    /// MIME types the extractor will claim via <see cref="ITextExtractor.CanHandle"/>.
    /// Defaults to the raster formats Tesseract's Leptonica backend supports reliably.
    /// </summary>
    public IList<string> AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/tiff",
        "image/bmp",
    ];
}
