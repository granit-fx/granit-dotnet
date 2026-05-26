using Granit.Html;
using Granit.TextExtraction.Exceptions;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Text;

/// <summary>
/// Extractor for <c>text/html</c> and <c>application/xhtml+xml</c>. Delegates to
/// the untrusted-profile <see cref="IHtmlToPlainTextConverter"/> for the DOM walk
/// and honours the truncation contract by trimming the resulting plain text to
/// <c>maxCharLength</c>.
/// </summary>
/// <remarks>
/// SSRF posture: the extractor consumes the keyed
/// <see cref="HtmlConverterKeys.Untrusted"/> converter so the DOM walker is
/// guaranteed to refuse external resource resolution regardless of provider.
/// </remarks>
public sealed class HtmlTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.html";

    private readonly GranitTextExtractionOptions _options;
    private readonly IHtmlToPlainTextConverter _converter;

    public HtmlTextExtractor(
        [FromKeyedServices(HtmlConverterKeys.Untrusted)] IHtmlToPlainTextConverter converter,
        IOptions<GranitTextExtractionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(options);
        _converter = converter;
        _options = options.Value;
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

        return contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        // Wrap the source for size-cap protection before feeding it to AngleSharp.
        // AngleSharp reads the full HTML into a string internally, so we materialise here.
        LimitedStream limited = new(source, _options.MaxBodySizeBytes);

        string html;
        using (StreamReader reader = new(limited, leaveOpen: true))
        {
            try
            {
                html = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TextExtractionException)
            {
                throw;
            }
        }

        string plain = await _converter.ConvertAsync(html, cancellationToken).ConfigureAwait(false);

        bool truncated = false;
        if (plain.Length > maxCharLength)
        {
            plain = plain[..maxCharLength];
            truncated = true;
        }

        return new TextExtractionResult(
            Content: plain,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: plain.Length,
            ExtractorName: ExtractorName,
            Confidence: ExtractionConfidence.Deterministic);
    }
}
