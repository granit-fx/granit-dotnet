namespace Granit.TextExtraction.Ocr.Tesseract.Options;

/// <summary>
/// Configuration options for the Tesseract OCR extractor. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class TesseractOcrOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction:Ocr:Tesseract";

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

    /// <summary>
    /// Filesystem root the Charlesw <c>Tesseract</c> NuGet should probe for the
    /// native <c>libleptonica</c> / <c>libtesseract</c> binaries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The NuGet uses its own <c>InteropDotNet.LibraryLoader</c> on Linux which does
    /// NOT honour <c>LD_LIBRARY_PATH</c> or the standard <c>dlopen</c> search paths.
    /// It only probes the app's <c>bin/</c> directory and a <c>CustomSearchPath</c>,
    /// and it appends a platform-name subdirectory (<c>x64</c> on amd64, <c>x86</c>
    /// on 32-bit) to the root before opening files. So with
    /// <c>LibrarySearchPath = "/opt/X"</c> the loader actually opens
    /// <c>/opt/X/x64/libleptonica-1.82.0.so</c>.
    /// </para>
    /// <para>
    /// Hosts installing <c>libtesseract</c> via apt MUST stage the canonical-name
    /// symlinks under a <c>&lt;root&gt;/x64/</c> subdirectory — mirroring the layout
    /// the NuGet uses natively for its Windows DLLs. The package README ships a
    /// ready-to-paste shell snippet.
    /// </para>
    /// <para>
    /// Defaults to <see langword="null"/>: the wrapper's built-in fallbacks
    /// (executing-assembly directory, AppDomain base, working dir) take over —
    /// useful for Windows hosts shipping the bundled DLLs in <c>bin/</c>.
    /// </para>
    /// </remarks>
    public string? LibrarySearchPath { get; set; }
}
