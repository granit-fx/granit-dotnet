using Granit.TextExtraction.Diagnostics;
using Granit.TextExtraction.Exceptions;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Internal;

/// <summary>
/// Default <see cref="ITextExtractionPipeline"/>. Iterates the registered extractors in DI
/// registration order, picks the first whose <see cref="ITextExtractor.CanHandle"/> returns
/// <c>true</c>, and falls back to <see cref="PlainTextExtractor"/> when none does.
/// </summary>
internal sealed class TextExtractionPipeline(
    IEnumerable<ITextExtractor> extractors,
    PlainTextExtractor fallback,
    TextExtractionMetrics metrics,
    IOptions<GranitTextExtractionOptions> options) : ITextExtractionPipeline
{
    private readonly IReadOnlyList<ITextExtractor> _extractors = [.. extractors];
    private readonly PlainTextExtractor _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    private readonly TextExtractionMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly GranitTextExtractionOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        ITextExtractor selected = SelectExtractor(contentType);

        try
        {
            TextExtractionResult result = await selected
                .ExtractAsync(source, contentType, _options.MaxExtractedCharLength, cancellationToken)
                .ConfigureAwait(false);

            _metrics.RecordSuccess(tenantId: null, result.ExtractorName, contentType);
            if (result.IsTruncated)
            {
                _metrics.RecordTruncated(tenantId: null, result.ExtractorName, contentType);
            }

            return result;
        }
        catch (TextExtractionException tex)
        {
            _metrics.RecordFailed(tenantId: null, selected.Name, contentType, tex.Reason);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordFailed(tenantId: null, selected.Name, contentType, ex.GetType().Name);
            throw;
        }
    }

    private ITextExtractor SelectExtractor(string contentType)
    {
        foreach (ITextExtractor extractor in _extractors)
        {
            if (extractor.CanHandle(contentType))
            {
                return extractor;
            }
        }

        // Universal fallback. RecordSkipped is fired so dashboards surface unmapped MIMEs even
        // though the pipeline still produces a (best-effort) plain-text result.
        _metrics.RecordSkipped(tenantId: null, contentType);
        return _fallback;
    }
}
