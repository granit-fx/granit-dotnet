using Granit.MultiTenancy;
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
/// <remarks>
/// Enforces two host-wide DoS caps that earlier shipped only as documentation:
/// <list type="bullet">
///   <item><see cref="GranitTextExtractionOptions.MaxConcurrentExtractions"/> via a
///   <see cref="SemaphoreSlim"/> — the (N+1)th in-flight call awaits a slot instead of
///   racing alongside the others.</item>
///   <item><see cref="GranitTextExtractionOptions.ExtractionTimeout"/> via a linked
///   <see cref="CancellationTokenSource"/> with <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>
///   — slow parsers (PdfPig recovery, frozen Tika sidecar, libtesseract on a pixel-cap
///   edge case) translate to <see cref="TextExtractionException"/> with reason
///   <c>extraction_timeout</c> instead of pinning a worker until process restart.</item>
/// </list>
/// </remarks>
internal sealed class TextExtractionPipeline : ITextExtractionPipeline, IDisposable
{
    private readonly IReadOnlyList<ITextExtractor> _extractors;
    private readonly PlainTextExtractor _fallback;
    private readonly TextExtractionMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly GranitTextExtractionOptions _options;
    private readonly SemaphoreSlim _gate;

    public TextExtractionPipeline(
        IEnumerable<ITextExtractor> extractors,
        PlainTextExtractor fallback,
        TextExtractionMetrics metrics,
        ICurrentTenant currentTenant,
        IOptions<GranitTextExtractionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(extractors);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(options);

        _extractors = [.. extractors];
        _fallback = fallback;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _options = options.Value;

        int slots = Math.Max(1, _options.MaxConcurrentExtractions);
        _gate = new SemaphoreSlim(slots, slots);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        ITextExtractor selected = SelectExtractor(contentType);
        string? tenantId = ResolveTenantId();

        // VULN-100: bound the number of in-flight extractions across the host process.
        // Done OUTSIDE the timeout link so queue time doesn't eat the per-extraction budget.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // VULN-101: link the caller's token with our extraction timeout so slow parsers
            // surface as a structured failure (extraction_timeout) instead of hanging the
            // semaphore slot indefinitely. A non-positive timeout means "disabled" — host
            // can opt down for offline batch jobs by setting ExtractionTimeout = 0.
            CancellationTokenSource? linkedCts = null;
            CancellationToken effectiveToken = cancellationToken;
            if (_options.ExtractionTimeout > TimeSpan.Zero)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(_options.ExtractionTimeout);
                effectiveToken = linkedCts.Token;
            }

            try
            {
                TextExtractionResult result = await selected
                    .ExtractAsync(source, contentType, _options.MaxExtractedCharLength, effectiveToken)
                    .ConfigureAwait(false);

                _metrics.RecordSuccess(tenantId, result.ExtractorName, contentType);
                if (result.IsTruncated)
                {
                    _metrics.RecordTruncated(tenantId, result.ExtractorName, contentType);
                }

                return result;
            }
            catch (OperationCanceledException) when (
                linkedCts is not null
                && linkedCts.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
            {
                // Our timeout fired (not the caller's token) — re-surface as a structured
                // pipeline failure so consumers can branch on `reason == "extraction_timeout"`.
                _metrics.RecordFailed(tenantId, selected.Name, contentType, "extraction_timeout");
                throw new TextExtractionException("extraction_timeout");
            }
            catch (TextExtractionException tex)
            {
                _metrics.RecordFailed(tenantId, selected.Name, contentType, tex.Reason);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordFailed(tenantId, selected.Name, contentType, ex.GetType().Name);
                throw;
            }
            finally
            {
                linkedCts?.Dispose();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private string? ResolveTenantId() =>
        _currentTenant.IsAvailable ? _currentTenant.Id?.ToString() : null;

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
        _metrics.RecordSkipped(ResolveTenantId(), contentType);
        return _fallback;
    }
}
