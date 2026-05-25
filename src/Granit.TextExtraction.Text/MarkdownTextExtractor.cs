using Granit.TextExtraction.Options;
using Markdig;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Text;

/// <summary>
/// Extractor for <c>text/markdown</c> and <c>text/x-markdown</c>. Renders to plain text via
/// <c>Markdig.Markdown.ToPlainText</c> configured with the advanced-extensions pipeline so
/// tables, footnotes, task lists and the like degrade to readable text rather than leaking
/// syntax noise.
/// </summary>
public sealed class MarkdownTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.markdown";

    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly GranitTextExtractionOptions _options;

    public MarkdownTextExtractor(IOptions<GranitTextExtractionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
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

        return contentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("text/x-markdown", StringComparison.OrdinalIgnoreCase);
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

        string raw;
        using (StreamReader reader = new(limited, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        string plain = Markdown.ToPlainText(raw, Pipeline);

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
