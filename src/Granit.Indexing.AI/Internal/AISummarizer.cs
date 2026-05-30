using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.AI.Redaction;
using Granit.AI.Sampling;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Granit.Indexing.AI.Schema;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.AI.Internal;

/// <summary>
/// <see cref="ISummarizer"/> backed by a one-shot LLM call via the <see cref="IStructuredCompletion"/>
/// primitive (ADR-064). Produces a SERP-style snippet for indexed entries whose <c>Summary</c>
/// would otherwise be null.
/// </summary>
/// <remarks>
/// <para><b>Graceful skip.</b> Every failure mode returns <c>null</c> rather than throwing.</para>
/// <para>
/// <b>Defence-in-depth (OWASP LLM01).</b> The primitive isolates the document in a sanitized
/// <c>&lt;data&gt;</c> block and pins the JSON schema; the detector keeps a per-tenant rate
/// limiter, optional PII redaction, and a hard cap on the returned summary length (over-length
/// responses are truncated and emit a metric).
/// </para>
/// </remarks>
internal sealed partial class AISummarizer : ISummarizer
{
    private readonly IStructuredCompletion _structuredCompletion;
    private readonly IAIAutoSummaryPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly IndexingAIOptions _options;
    private readonly IndexingAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AISummarizer> _logger;

    public AISummarizer(
        IStructuredCompletion structuredCompletion,
        IAIAutoSummaryPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<IndexingAIOptions> options,
        IndexingAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AISummarizer> logger)
    {
        ArgumentNullException.ThrowIfNull(structuredCompletion);
        ArgumentNullException.ThrowIfNull(promptBuilder);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(logger);
        _structuredCompletion = structuredCompletion;
        _promptBuilder = promptBuilder;
        _rateLimiter = rateLimiter;
        _redactor = redactor;
        _options = options.Value;
        _metrics = metrics;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> SummarizeAsync(
        string content,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        // Language hinting is left to the model's own detection / a host-supplied prompt builder
        // — kept off the public seam to avoid a per-call parameter explosion.
        _ = language;

        string? tenantId = _currentTenant.Id?.ToString();
        string bucketKey = $"indexing_summarizer:{tenantId ?? "global"}";

        bool admitted = await _rateLimiter
            .TryAcquireAsync(bucketKey, _options.MaxAICallsPerHourPerTenant, cancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            _metrics.RecordSummarizerThrottled(tenantId);
            LogThrottled(tenantId ?? "global");
            return null;
        }

        string sample = AIContentSampler.TruncateOnCodePoint(content, _options.MaxContentLength);

        if (_options.RedactPIIBeforeLLMCall)
        {
            sample = _redactor.Redact(sample);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _metrics.RecordSummarizerAttempted(tenantId);

        var request = new StructuredCompletionRequest
        {
            Instruction = _promptBuilder.BuildInstruction(_options.MaxSummaryLength),
            Content = sample,
            ContentLabel = "Document",
            WorkspaceName = _options.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<SummaryResponse> result = await _structuredCompletion
                .CompleteAsync<SummaryResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case StructuredCompletionStatus.Succeeded:
                    return CapSummary(result.Value!, tenantId);

                case StructuredCompletionStatus.ModelRefused:
                case StructuredCompletionStatus.SchemaViolation:
                    _metrics.RecordSummarizerInjection(tenantId);
                    return null;

                case StructuredCompletionStatus.TransportFailure:
                default:
                    _metrics.RecordSummarizerFailed(tenantId, "transport");
                    LogTransportFailure(tenantId ?? "global", result.Status.ToString());
                    return null;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordSummarizerFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? "global", _options.TimeoutSeconds);
            return null;
        }
    }

    private string? CapSummary(SummaryResponse parsed, string? tenantId)
    {
        if (string.IsNullOrEmpty(parsed.Summary))
        {
            return null;
        }

        if (parsed.Summary.Length <= _options.MaxSummaryLength)
        {
            return parsed.Summary;
        }

        _metrics.RecordSummarizerTruncated(tenantId);
        return AIContentSampler.TruncateOnCodePoint(parsed.Summary, _options.MaxSummaryLength);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer throttled for tenant {TenantId}")]
    private partial void LogThrottled(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer timed out for tenant {TenantId} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string tenantId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer transport failure for tenant {TenantId} ({Status})")]
    private partial void LogTransportFailure(string tenantId, string status);
}
