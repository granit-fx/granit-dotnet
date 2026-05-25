using System.Text;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction;

/// <summary>
/// Default extractor for <c>text/plain</c> content and the universal fallback when no
/// concrete extractor claims a content type. Reads <see cref="Stream"/> as UTF-8, honouring
/// a leading BOM, and stops once the produced text reaches <c>maxCharLength</c>.
/// </summary>
public sealed class PlainTextExtractor(IOptions<GranitTextExtractionOptions> options) : ITextExtractor
{
    private const string PlainText = "text/plain";

    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.plain-text";

    private readonly GranitTextExtractionOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    /// <remarks>
    /// Claims any content type that starts with <c>text/</c>. The pipeline ALSO uses this
    /// extractor as the universal fallback when no other extractor claims a binary content
    /// type — that path lives in the pipeline, not here.
    /// </remarks>
    public bool CanHandle(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        // text/plain wins; the pipeline routes "unknown" content types to this extractor itself.
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
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

        LimitedStream limited = new(source, _options.MaxBodySizeBytes);

        // detectEncodingFromByteOrderMarks: true → honour UTF-8 / UTF-16 BOM transparently.
        // leaveOpen: true → caller owns the source lifetime.
        using StreamReader reader = new(
            limited,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        StringBuilder builder = new(Math.Min(maxCharLength, 4096));
        char[] buffer = new char[4096];
        bool truncated = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            int remaining = maxCharLength - builder.Length;
            if (read >= remaining)
            {
                builder.Append(buffer, 0, remaining);
                truncated = true;
                break;
            }

            builder.Append(buffer, 0, read);
        }

        string text = builder.ToString();
        return new TextExtractionResult(
            Content: text,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: text.Length,
            ExtractorName: ExtractorName);
    }

    /// <summary>
    /// Stable singleton MIME used when callers don't know the source content type and want
    /// to force the plain-text path. Kept as a constant so consumers don't allocate strings.
    /// </summary>
    internal const string FallbackContentType = PlainText;
}
