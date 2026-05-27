using System.Text.Json;
using Granit.AI;
using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.Redaction;
using Granit.AI.Extraction.Sampling;
using Granit.Indexing.AI.Diagnostics;
using Granit.Indexing.AI.Options;
using Granit.Indexing.AI.Prompts;
using Granit.Indexing.AI.Schema;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.AI.Internal;

/// <summary>
/// <see cref="IAutoTagger"/> backed by a one-shot LLM call. Returns a subset of the
/// consumer-supplied candidate tag universe.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ Suggestion-only UX contract.</b> User-facing UI MUST require explicit
/// confirmation before applying suggested tags. The 'suggestion-only' guarantee is a
/// UX contract — in bulk-approve flows, this defence becomes ineffective.
/// </para>
/// <para>
/// <b>Server-side intersection (non-negotiable).</b> The auto-tagger filters the LLM
/// response through <c>response.Tags.Intersect(candidates)</c> BEFORE returning. Out-of-
/// candidate IDs are dropped silently — they cannot reach the consumer module under any
/// circumstance. A prompt-injection payload that proposes <c>"delete-everything"</c>
/// gets discarded, and the <c>granit.indexing.ai.autotag.out_of_candidate</c> metric
/// captures the attempt for ops alerting.
/// </para>
/// <para>
/// <b>Defence-in-depth against prompt injection (OWASP LLM01 / LLM02).</b> Three layers
/// in addition to the intersection:
/// </para>
/// <list type="bullet">
///   <item>Instruction-isolation wrapping via <see cref="IAutoTagPromptBuilder"/>.</item>
///   <item>JSON-schema pinning via <c>ChatResponseFormat.ForJsonSchema&lt;AutoTagResponse&gt;</c>.</item>
///   <item>Optional PII redaction through <see cref="IAIContentRedactor"/> when
///         <see cref="IndexingAIOptions.RedactPIIBeforeLLMCall"/> is set.</item>
/// </list>
/// <para>
/// <b>Graceful skip.</b> Every failure mode — rate-limit denial, timeout, transport
/// error, schema reject, empty candidate list — returns an empty list rather than
/// throwing. The caller persists the entry without auto-tags; manual tagging still
/// works.
/// </para>
/// </remarks>
internal sealed partial class AIAutoTagger : IAutoTagger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly ChatOptions StructuredOutputOptions = new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<AutoTagResponse>(),
    };

    private readonly IAIChatClientFactory _chatClientFactory;
    private readonly IAutoTagPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly IndexingAIOptions _options;
    private readonly IndexingAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AIAutoTagger> _logger;

    public AIAutoTagger(
        IAIChatClientFactory chatClientFactory,
        IAutoTagPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<IndexingAIOptions> options,
        IndexingAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AIAutoTagger> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClientFactory);
        ArgumentNullException.ThrowIfNull(promptBuilder);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(logger);
        _chatClientFactory = chatClientFactory;
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

        string bucketKey = $"indexing_autotag:{tenantId ?? "global"}";
        bool admitted = await _rateLimiter
            .TryAcquireAsync(bucketKey, _options.MaxAutoTagCallsPerHourPerTenant, cancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            _metrics.RecordAutoTaggerThrottled(tenantId);
            LogThrottled(tenantId ?? "global");
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

        try
        {
            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await _chatClientFactory
                .CreateAsync(_options.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            IReadOnlyList<ChatMessage> messages = _promptBuilder.Build(sample, candidateList, effectiveCap);

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, StructuredOutputOptions, linkedCts.Token)
                .ConfigureAwait(false);

            return Intersect(response.Text, candidateList, effectiveCap, tenantId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordAutoTaggerFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? "global", _options.TimeoutSeconds);
            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            _metrics.RecordAutoTaggerInjection(tenantId);
            return [];
        }
        catch (Exception ex)
        {
            // Never log ex.Message — providers can echo the prompt payload (PII) in 4xx
            // exception messages, which would bypass the IAIContentRedactor seam.
            _metrics.RecordAutoTaggerFailed(tenantId, "transport");
            LogTransportFailure(tenantId ?? "global", ex.GetType().Name);
            return [];
        }
    }

    private List<string> Intersect(
        string? responseText,
        IReadOnlyList<string> candidates,
        int cap,
        string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _metrics.RecordAutoTaggerInjection(tenantId);
            return [];
        }

        AutoTagResponse? parsed = JsonSerializer.Deserialize<AutoTagResponse>(responseText, SerializerOptions);
        if (parsed is null || parsed.Tags.Length == 0)
        {
            return [];
        }

        // Server-side intersection — the non-negotiable safety net. Hash set lookup
        // keeps the O(n + m) cost bounded even when the candidate list is large.
        HashSet<string> allowed = new(candidates, StringComparer.Ordinal);
        List<string> kept = new(Math.Min(parsed.Tags.Length, cap));
        HashSet<string> deduped = new(StringComparer.Ordinal);
        int dropped = 0;

        foreach (string tag in parsed.Tags)
        {
            if (string.IsNullOrEmpty(tag))
            {
                dropped++;
                continue;
            }
            if (!allowed.Contains(tag))
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
            LogOutOfCandidate(tenantId ?? "global", dropped);
        }

        return kept;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger throttled for tenant {TenantId}")]
    private partial void LogThrottled(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger timed out for tenant {TenantId} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string tenantId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger transport failure for tenant {TenantId} (exception type: {ExceptionType})")]
    private partial void LogTransportFailure(string tenantId, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI auto-tagger dropped {DroppedCount} out-of-candidate tag(s) for tenant {TenantId}")]
    private partial void LogOutOfCandidate(string tenantId, int droppedCount);
}
