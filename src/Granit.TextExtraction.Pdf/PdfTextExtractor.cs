using System.Text;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Granit.TextExtraction.Pdf;

/// <summary>
/// Extractor for <c>application/pdf</c>. Reads the PDF with PdfPig, runs the
/// content-order text extractor on each page, and joins the result with blank lines.
/// Falls back to raw word iteration when the content-order extractor throws on
/// malformed page content — keeps degraded PDFs indexable.
/// </summary>
/// <remarks>
/// Password-protected and malformed PDFs are converted to an empty
/// <see cref="TextExtractionResult"/> with <see cref="TextExtractionResult.IsTruncated"/>
/// set to <c>true</c> (so consumers can flag the document as partial) rather than throwing.
/// This matches the framework's "never throw on cap breach" philosophy for soft failures.
/// </remarks>
public sealed partial class PdfTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.pdf";

    private const string Pdf = "application/pdf";

    private readonly GranitTextExtractionOptions _options;
    private readonly ILogger<PdfTextExtractor> _logger;

    public PdfTextExtractor(IOptions<GranitTextExtractionOptions> options, ILogger<PdfTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
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

        // PdfPig's PdfDocument.Open accepts a byte[] or a Stream. We materialise via
        // LimitedStream first so the size cap is enforced before any parser allocations.
        byte[] bytes = await ReadAllBytesAsync(source, _options.MaxBodySizeBytes, cancellationToken)
            .ConfigureAwait(false);

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
            StringBuilder sb = new(Math.Min(maxCharLength, 8192));
            bool truncated = false;
            bool usedFallback = false;

            for (int i = 1; i <= document.NumberOfPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Page page = document.GetPage(i);
                (string pageText, bool pageFallback) = ExtractPageText(page);
                usedFallback |= pageFallback;

                if (sb.Length > 0)
                {
                    sb.Append('\n');
                    sb.Append('\n');
                }

                int remaining = maxCharLength - sb.Length;
                if (pageText.Length >= remaining)
                {
                    sb.Append(pageText, 0, Math.Max(remaining, 0));
                    truncated = true;
                    break;
                }

                sb.Append(pageText);
            }

            string content = sb.ToString();
            return new TextExtractionResult(
                Content: content,
                DetectedLanguage: null,
                IsTruncated: truncated,
                CharCount: content.Length,
                ExtractorName: ExtractorName,
                // Heuristic when any page tripped layout analysis — the raw-word fallback can
                // mis-order tokens (multi-column → linearised). Consumers can downgrade trust
                // (e.g. flag as "partial extraction").
                Confidence: usedFallback
                    ? ExtractionConfidence.Heuristic
                    : ExtractionConfidence.Deterministic);
        }
    }

    private (string Text, bool UsedFallback) ExtractPageText(Page page)
    {
        try
        {
            return (ContentOrderTextExtractor.GetText(page) ?? string.Empty, false);
        }
        catch (Exception ex)
        {
            // Some malformed PDFs trip the layout analyser even though the page parses fine —
            // fall back to raw words so the document stays indexable instead of being dropped.
            LogPageOrderingFallback(ex, page.Number);
            return (string.Join(' ', page.GetWords().Select(w => w.Text)), true);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        LimitedStream limited = new(source, maxBytes);
        await using MemoryStream buffer = new();
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

    // PdfPig doesn't expose a single "malformed PDF" base type. Any exception whose
    // namespace lives under UglyToad.PdfPig.* originates from the parser and means the
    // document is unusable — treat it as a soft-skip rather than propagating up to the
    // pipeline. (Cancellation tokens already short-circuit before reaching this catch.)
    private static bool IsMalformedPdfException(Exception ex) =>
        ex.GetType().Namespace?.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal) == true;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PdfTextExtractor skipped an encrypted PDF.")]
    private partial void LogEncryptedPdfSkipped(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "PdfTextExtractor skipped a malformed PDF.")]
    private partial void LogMalformedPdfSkipped(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "PdfTextExtractor falling back to raw word iteration on page {PageNumber}.")]
    private partial void LogPageOrderingFallback(Exception exception, int pageNumber);
}
