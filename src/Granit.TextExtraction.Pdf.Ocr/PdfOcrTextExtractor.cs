using System.Text;
using Granit.TextExtraction.Options;
using Granit.TextExtraction.Pdf.Ocr.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Granit.TextExtraction.Pdf.Ocr;

/// <summary>
/// PDF extractor that augments PdfPig's text-mode reader with an OCR fallback for
/// pages where the embedded text layer is empty or near-empty — typically scanned
/// documents. Pages with usable native text bypass OCR entirely.
/// </summary>
/// <remarks>
/// <para>
/// Page-by-page algorithm: PdfPig's <c>ContentOrderTextExtractor</c> runs first; if
/// its output is below the configured character threshold the page is rasterised
/// (one bitmap at a time, via <see cref="IPdfRasterizer"/>) and routed to the
/// host's <c>image/png</c> OCR extractor. The page-level interleave preserves order
/// in mixed documents (native text + scanned inserts).
/// </para>
/// <para>
/// Replaces the base <see cref="PdfTextExtractor"/> when the host opts in — see
/// <see cref="Extensions.ServiceCollectionExtensions.AddGranitTextExtractionPdfOcr"/>.
/// </para>
/// </remarks>
public sealed partial class PdfOcrTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.pdf.ocr";

    private const string Pdf = "application/pdf";
    private const string ImagePng = "image/png";

    private readonly IPdfRasterizer _rasterizer;
    private readonly IServiceProvider _services;
    private readonly GranitTextExtractionOptions _extractionOptions;
    private readonly PdfOcrOptions _ocrOptions;
    private readonly ILogger<PdfOcrTextExtractor> _logger;

    /// <summary>
    /// Resolves the image/png OCR extractor lazily via <paramref name="services"/> to
    /// avoid the DI cycle that constructor-injecting <c>IEnumerable&lt;ITextExtractor&gt;</c>
    /// would create — this type IS one of the registered <c>ITextExtractor</c>s.
    /// </summary>
    public PdfOcrTextExtractor(
        IPdfRasterizer rasterizer,
        IServiceProvider services,
        IOptions<GranitTextExtractionOptions> extractionOptions,
        IOptions<PdfOcrOptions> ocrOptions,
        ILogger<PdfOcrTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(extractionOptions);
        ArgumentNullException.ThrowIfNull(ocrOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _rasterizer = rasterizer;
        _services = services;
        _extractionOptions = extractionOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.Equals(Pdf, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        byte[] bytes = await ReadAllBytesAsync(source, _extractionOptions.MaxBodySizeBytes, cancellationToken)
            .ConfigureAwait(false);

        // Discover the image/png OCR extractor lazily so we don't fail at host-build
        // time when only the text-mode PDF path is needed (e.g. during development).
        ITextExtractor? ocrExtractor = ResolveOcrExtractor();
        if (ocrExtractor is null)
        {
            LogNoOcrExtractorRegistered();
            // Fall through to native-only extraction — same shape as PdfTextExtractor.
        }

        PdfDocument document;
        try
        {
            document = PdfDocument.Open(bytes);
        }
        catch (PdfDocumentEncryptedException ex)
        {
            LogEncryptedPdfSkipped(ex);
            return Skipped();
        }
        catch (Exception ex) when (IsMalformedPdfException(ex))
        {
            LogMalformedPdfSkipped(ex);
            return Skipped();
        }

        using (document)
        {
            ReadOnlyMemory<byte> pdfMemory = bytes;
            StringBuilder sb = new(Math.Min(maxCharLength, 8192));
            bool truncated = false;
            bool usedOcr = false;
            bool usedFallback = false;

            int pageCount = document.NumberOfPages;
            int rasterisableCount = Math.Min(pageCount, _ocrOptions.MaxPagesToRasterise);
            if (pageCount > rasterisableCount)
            {
                LogPageCountClamped(pageCount, rasterisableCount);
                truncated = true;
            }

            for (int i = 1; i <= rasterisableCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Page page = document.GetPage(i);
                PageAppendResult result = await AppendPageTextAsync(
                    sb, page, pageNumber: i, ocrExtractor, pdfMemory, maxCharLength, cancellationToken)
                    .ConfigureAwait(false);

                usedFallback |= result.UsedFallback;
                usedOcr |= result.UsedOcr;
                if (result.Truncated)
                {
                    truncated = true;
                    break;
                }
            }

            string content = sb.ToString();
            return new TextExtractionResult(
                Content: content,
                DetectedLanguage: null,
                IsTruncated: truncated,
                CharCount: content.Length,
                ExtractorName: ExtractorName,
                // Any OCR or layout-fallback page drops the result to Heuristic — OCR is
                // statistical even on best-case fixtures and the raw-word fallback can
                // mis-order tokens.
                Confidence: (usedOcr || usedFallback)
                    ? ExtractionConfidence.Heuristic
                    : ExtractionConfidence.Deterministic);
        }
    }

    private readonly record struct PageAppendResult(bool Truncated, bool UsedOcr, bool UsedFallback);

    // Extracts one page (native text, falling back to OCR for scanned pages), appends it to
    // <paramref name="sb"/> with a blank-line separator, and reports whether the output cap was hit.
    private async Task<PageAppendResult> AppendPageTextAsync(
        StringBuilder sb, Page page, int pageNumber, ITextExtractor? ocrExtractor,
        ReadOnlyMemory<byte> pdfMemory, int maxCharLength, CancellationToken cancellationToken)
    {
        (string nativeText, bool pageFallback) = ExtractPageText(page);

        string pageText = nativeText;
        bool usedOcr = false;
        bool scanned = nativeText.Length < _ocrOptions.MinNativeCharsPerPage;
        if (scanned && ocrExtractor is not null)
        {
            string? ocrText = await TryOcrPageAsync(
                ocrExtractor, pdfMemory, pageIndex: pageNumber - 1, page, maxCharLength, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(ocrText))
            {
                pageText = ocrText;
                usedOcr = true;
            }
        }

        if (sb.Length > 0)
        {
            sb.Append('\n');
            sb.Append('\n');
        }

        int remaining = maxCharLength - sb.Length;
        if (pageText.Length >= remaining)
        {
            sb.Append(pageText, 0, Math.Max(remaining, 0));
            return new PageAppendResult(Truncated: true, usedOcr, pageFallback);
        }

        sb.Append(pageText);
        return new PageAppendResult(Truncated: false, usedOcr, pageFallback);
    }

    private async Task<string?> TryOcrPageAsync(
        ITextExtractor ocrExtractor,
        ReadOnlyMemory<byte> pdfBytes,
        int pageIndex,
        Page page,
        int maxCharLength,
        CancellationToken cancellationToken)
    {
        // Pixel-bomb defence — PdfPig knows the page dimensions at PDF-units; multiply
        // by DPI/72 to get the bitmap surface and reject before we burn a render call.
        double widthInches = page.Width / 72.0;
        double heightInches = page.Height / 72.0;
        long pixels = (long)Math.Round(widthInches * _ocrOptions.RenderDpi)
            * (long)Math.Round(heightInches * _ocrOptions.RenderDpi);
        if (pixels > _ocrOptions.MaxPagePixels)
        {
            LogPageRejectedByPixelCap(page.Number, pixels, _ocrOptions.MaxPagePixels);
            return null;
        }

        byte[] pngBytes;
        try
        {
            pngBytes = await _rasterizer.RasterisePageAsync(
                pdfBytes, pageIndex, _ocrOptions.RenderDpi, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogRasterisationFailed(ex, page.Number);
            return null;
        }

        try
        {
            using MemoryStream pngStream = new(pngBytes);
            TextExtractionResult result = await ocrExtractor.ExtractAsync(
                pngStream, ImagePng, maxCharLength, cancellationToken).ConfigureAwait(false);
            return result.Content;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogOcrFailed(ex, page.Number);
            return null;
        }
    }

    private ITextExtractor? ResolveOcrExtractor()
    {
        // Lazy resolution avoids the constructor-time DI cycle (this type IS an
        // ITextExtractor). Skip self defensively even though CanHandle returns false
        // for image/png — a future refactor that broadens CanHandle would otherwise
        // recurse silently.
        foreach (ITextExtractor extractor in _services.GetServices<ITextExtractor>())
        {
            if (ReferenceEquals(extractor, this))
            {
                continue;
            }

            if (extractor.CanHandle(ImagePng))
            {
                return extractor;
            }
        }
        return null;
    }

    private static (string Text, bool UsedFallback) ExtractPageText(Page page)
    {
        try
        {
            return (ContentOrderTextExtractor.GetText(page) ?? string.Empty, false);
        }
        catch (Exception)
        {
            return (string.Join(' ', page.GetWords().Select(w => w.Text)), true);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        LimitedStream limited = new(source, maxBytes);
        using MemoryStream buffer = new();
        await limited.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static TextExtractionResult Skipped() =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: ExtractorName,
            Confidence: ExtractionConfidence.Deterministic);

    private static bool IsMalformedPdfException(Exception ex) =>
        ex.GetType().Namespace?.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal) == true;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PdfOcrTextExtractor skipped an encrypted PDF.")]
    private partial void LogEncryptedPdfSkipped(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PdfOcrTextExtractor skipped a malformed PDF.")]
    private partial void LogMalformedPdfSkipped(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "PdfOcrTextExtractor: no image/png ITextExtractor registered — scanned pages will be skipped. Wire AddTesseractOcrExtractor() (or another image extractor) before AddGranitTextExtractionPdfOcr().")]
    private partial void LogNoOcrExtractorRegistered();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PdfOcrTextExtractor clamped page count {Total} to {Cap} (MaxPagesToRasterise).")]
    private partial void LogPageCountClamped(int total, int cap);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "PdfOcrTextExtractor rejected page {PageNumber}: {ComputedPixels} pixels exceeds cap {Cap}.")]
    private partial void LogPageRejectedByPixelCap(int pageNumber, long computedPixels, long cap);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "PdfOcrTextExtractor rasterisation failed for page {PageNumber}; soft-skipping the page.")]
    private partial void LogRasterisationFailed(Exception exception, int pageNumber);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "PdfOcrTextExtractor OCR failed for page {PageNumber}; soft-skipping the page.")]
    private partial void LogOcrFailed(Exception exception, int pageNumber);
}
