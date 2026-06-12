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
/// <see cref="IAutoTagger"/> backed by a one-shot LLM call via the <see cref="IStructuredCompletion"/>
/// primitive (ADR-064). Returns a subset of the consumer-supplied candidate tag universe.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ Suggestion-only UX contract.</b> User-facing UI MUST require explicit confirmation
/// before applying suggested tags.
/// </para>
/// <para>
/// <b>Server-side intersection (non-negotiable).</b> The auto-tagger filters the response
/// through <c>candidates</c> BEFORE returning. Out-of-candidate IDs are dropped silently and
/// captured on the <c>granit.indexing.ai.autotag.out_of_candidate</c> metric — a hostile prompt
/// that proposes <c>"delete-everything"</c> cannot reach the consumer.
/// </para>
/// <para>
/// <b>Defence-in-depth (OWASP LLM01 / LLM02).</b> In addition to the intersection: the dev
/// instruction lists the authoritative candidate set, the primitive isolates the document in a
/// sanitized <c>&lt;data&gt;</c> block and pins the JSON schema, and an optional PII redaction
/// pass runs when <see cref="IndexingAIOptions.RedactPIIBeforeLLMCall"/> is set.
/// </para>
/// <para><b>Graceful skip.</b> Every failure mode returns an empty list rather than throwing.</para>
/// </remarks>
internal sealed partial class AIAutoTagger : IAutoTagger
{
    private readonly IStructuredCompletion _structuredCompletion;
    private readonly IAutoTagPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly IndexingAIOptions _options;
    private readonly IndexingAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AIAutoTagger> _logger;

    public AIAutoTagger(
        IStructuredCompletion structuredCompletion,
        IAutoTagPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<IndexingAIOptions> options,
        IndexingAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AIAutoTagger> logger)
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
    public async Task<IReadOnlyList<string>> TagAsync(
        string content,
        ITagCandidateProvider candidates,
        int maxTags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(candidates);

        if (string.IsNullOrWhiteSpace(content) || maxTags <= 0)
        {
            return [];
        }

        string? tenantId = _currentTenant.Id?.ToString();

        IReadOnlyList<string> candidateList = await candidates
            .GetCandidatesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateList.Count == 0)
        {
            return [];
        }

        string bucketKey = $"indexing_autotag:{tenantId ?? IndexingAIMetrics.GlobalTenant}";
        bool admitted = await _rateLimiter
            .TryAcquireAsync(bucketKey, _options.MaxAutoTagCallsPerHourPerTenant, cancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            _metrics.RecordAutoTaggerThrottled(tenantId);
            LogThrottled(tenantId ?? IndexingAIMetrics.GlobalTenant);
            return [];
        }

        string sample = AIContentSampler.TruncateOnCodePoint(content, _options.MaxAutoTagContentLength);

        if (_options.RedactPIIBeforeLLMCall)
        {
            sample = _redactor.Redact(sample);
        }

        int effectiveCap = Math.Min(maxTags, _options.MaxAutoTagsReturned);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _metrics.RecordAutoTaggerAttempted(tenantId);

        var request = new StructuredCompletionRequest
        {
            Instruction = _promptBuilder.BuildInstruction(candidateList, effectiveCap),
            Content = sample,
            ContentLabel = "Document",
            WorkspaceName = _options.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<AutoTagResponse> result = await _structuredCompletion
                .CompleteAsync<AutoTagResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case StructuredCompletionStatus.Succeeded:
                    return Intersect(result.Value!, candidateList, effectiveCap, tenantId);

                case StructuredCompletionStatus.ModelRefused:
                case StructuredCompletionStatus.SchemaViolation:
                    _metrics.RecordAutoTaggerInjection(tenantId);
                    return [];

                default:
                    _metrics.RecordAutoTaggerFailed(tenantId, "transport");
                    LogTransportFailure(tenantId ?? IndexingAIMetrics.GlobalTenant, result.Status.ToString());
                    return [];
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordAutoTaggerFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? IndexingAIMetrics.GlobalTenant, _options.TimeoutSeconds);
            return [];
        }
    }

    private List<string> Intersect(
        AutoTagResponse parsed,
        IReadOnlyList<string> candidates,
        int cap,
        string? tenantId)
    {
        if (parsed.Tags.Length == 0)
        {
            return [];
        }

        // Server-side intersection — the non-negotiable safety net.
        HashSet<string> allowed = new(candidates, StringComparer.Ordinal);
        List<string> kept = new(Math.Min(parsed.Tags.Length, cap));
        HashSet<string> deduped = new(StringComparer.Ordinal);
        int dropped = 0;

        foreach (string tag in parsed.Tags)
        {
            if (string.IsNullOrEmpty(tag) || !allowed.Contains(tag))
            {
                dropped++;
                continue;
            }
            if (deduped.Add(tag))
            {
                kept.Add(tag);
                if (kept.Count >= cap)
                {
                    break;
                }
            }
        }

        if (dropped > 0)
        {
            _metrics.RecordAutoTaggerOutOfCandidate(tenantId, dropped);
            LogOutOfCandidate(tenantId ?? IndexingAIMetrics.GlobalTenant, dropped);
        }

        return kept;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger throttled for tenant {TenantId}")]
    private partial void LogThrottled(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger timed out for tenant {TenantId} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string tenantId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger transport failure for tenant {TenantId} ({Status})")]
    private partial void LogTransportFailure(string tenantId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger dropped {DroppedCount} out-of-candidate tag(s) for tenant {TenantId}")]
    private partial void LogOutOfCandidate(string tenantId, int droppedCount);
}
