using Granit.TextExtraction.Ocr.Tesseract.Options;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace Granit.TextExtraction.Ocr.Tesseract;

/// <summary>
/// Extractor that runs the native <c>libtesseract</c> engine against raster images and
/// returns the decoded text. Pure on-prem path — no document bytes leave the host.
/// </summary>
/// <remarks>
/// <para>
/// Deployment cost: the host must ship <c>libtesseract5</c> + the matching
/// <c>*.traineddata</c> files (~10–30 MB per language). On Linux:
/// <c>apt-get install -y libtesseract5 tesseract-ocr-eng tesseract-ocr-fra</c>.
/// Point <see cref="TesseractOcrOptions.DataPath"/> at the directory.
/// </para>
/// <para>
/// Pixel-bomb defence (VULN-001): the image header is read BEFORE any decode and the
/// surface (<c>width × height</c>) is checked against
/// <see cref="TesseractOcrOptions.MaxImagePixels"/>. Oversized images soft-skip without
/// allocating a decoded buffer.
/// </para>
/// <para>
/// Scanned PDFs (<c>application/pdf</c>) are intentionally NOT handled here — page
/// rasterisation is a separate concern (Magick.NET / Ghostscript / PDFium) and is
/// tracked as a follow-up.
/// </para>
/// </remarks>
public sealed partial class TesseractOcrExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.ocr-tesseract";

    private readonly ITesseractRecognizer _recognizer;
    private readonly GranitTextExtractionOptions _extractionOptions;
    private readonly TesseractOcrOptions _ocrOptions;
    private readonly ILogger<TesseractOcrExtractor> _logger;
    private readonly HashSet<string> _allowedContentTypes;

    public TesseractOcrExtractor(
        ITesseractRecognizer recognizer,
        IOptions<GranitTextExtractionOptions> extractionOptions,
        IOptions<TesseractOcrOptions> ocrOptions,
        ILogger<TesseractOcrExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentNullException.ThrowIfNull(extractionOptions);
        ArgumentNullException.ThrowIfNull(ocrOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _recognizer = recognizer;
        _extractionOptions = extractionOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
        _allowedContentTypes = new HashSet<string>(
            _ocrOptions.AllowedContentTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return _allowedContentTypes.Contains(contentType);
    }

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        // VULN-001 byte cap — Tesseract's Pix.LoadFromMemory will decode whatever we hand
        // it, so the body-size cap from the base module is the first line of defence.
        byte[] bytes = await ReadAllBytesAsync(
            source, _extractionOptions.MaxBodySizeBytes, cancellationToken).ConfigureAwait(false);

        // VULN-001 pixel-bomb defence — identify dimensions from the format header without
        // decoding. A 100 KB PNG can claim 100 000 × 100 000 pixels (~40 GB RGBA buffer);
        // we reject those before they hit Leptonica.
        IImageInfo? info;
        try
        {
            info = Image.Identify(bytes);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            LogUnreadableImageSkipped(ex, contentType);
            return Skipped();
        }

        if (info is null)
        {
            LogUnreadableImageSkipped(null, contentType);
            return Skipped();
        }

        long pixels = (long)info.Width * info.Height;
        if (pixels > _ocrOptions.MaxImagePixels)
        {
            LogImageRejectedByPixelCap(info.Width, info.Height, _ocrOptions.MaxImagePixels);
            return Skipped();
        }

        string text;
        try
        {
            text = await _recognizer.RecognizeAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogRecognitionFailed(ex);
            return Skipped();
        }

        return Truncate(text, maxCharLength);
    }

    private static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        LimitedStream limited = new(source, maxBytes);
        using MemoryStream buffer = new();
        await limited.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static TextExtractionResult Truncate(string content, int maxCharLength)
    {
        bool truncated = content.Length > maxCharLength;
        string output = truncated ? content[..maxCharLength] : content;

        return new TextExtractionResult(
            Content: output,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: output.Length,
            ExtractorName: ExtractorName,
            // Tesseract is a deterministic OCR engine: same image bytes, same `tessdata`,
            // same recognised text. No model in the loop — Heuristic would overstate the risk.
            Confidence: ExtractionConfidence.Deterministic);
    }

    private static TextExtractionResult Skipped() =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: ExtractorName,
            Confidence: ExtractionConfidence.Deterministic);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "TesseractOcrExtractor skipped an unreadable image (content type {ContentType}).")]
    private partial void LogUnreadableImageSkipped(Exception? exception, string contentType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TesseractOcrExtractor rejected an image of {Width}x{Height} pixels (cap {Cap}).")]
    private partial void LogImageRejectedByPixelCap(int width, int height, long cap);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TesseractOcrExtractor recognition failed.")]
    private partial void LogRecognitionFailed(Exception exception);
}
