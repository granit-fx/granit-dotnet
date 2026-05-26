using System.Text.Json;
using Granit.AI;
using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.Redaction;
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
/// <see cref="ISummarizer"/> backed by a one-shot LLM call. Produces a SERP-style
/// snippet for indexed entries whose <c>Summary</c> would otherwise be null.
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful skip.</b> Every failure mode — rate-limit denial, timeout, transport
/// error, schema reject — returns <c>null</c> rather than throwing. The caller
/// persists the entry without a summary; the search pipeline still ranks by the body.
/// </para>
/// <para>
/// <b>Defence-in-depth against prompt injection (OWASP LLM01).</b> Instruction-isolation
/// wrapping via <see cref="IAIAutoSummaryPromptBuilder"/>, JSON-schema pinning via
/// <c>ChatResponseFormat.ForJsonSchema</c>, and a hard cap on the returned summary
/// length. Over-length responses are truncated and emit a metric so the host can spot
/// a prompt-adherence drift.
/// </para>
/// </remarks>
internal sealed partial class AISummarizer : ISummarizer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly ChatOptions StructuredOutputOptions = new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<SummaryResponse>(),
    };

    private readonly IAIChatClientFactory _chatClientFactory;
    private readonly IAIAutoSummaryPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly IndexingAIOptions _options;
    private readonly IndexingAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AISummarizer> _logger;

    public AISummarizer(
        IAIChatClientFactory chatClientFactory,
        IAIAutoSummaryPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<IndexingAIOptions> options,
        IndexingAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AISummarizer> logger)
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

        // The default prompt builder folds language hinting into the wrapped system
        // instruction implicitly via the model's own language detection. Hosts wiring a
        // language-aware prompt builder can read the hint from a downstream context
        // (e.g. AsyncLocal) — kept off the public seam to avoid a per-call parameter
        // explosion as new AI hints get added in I-F3.3 / I-F4.x.
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

        string sample = content.Length > _options.MaxContentLength
            ? content[.._options.MaxContentLength]
            : content;

        if (_options.RedactPIIBeforeLLMCall)
        {
            sample = _redactor.Redact(sample);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _metrics.RecordSummarizerAttempted(tenantId);

        try
        {
            IChatClient chatClient = await _chatClientFactory
                .CreateAsync(_options.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            IReadOnlyList<ChatMessage> messages = _promptBuilder.Build(sample, _options.MaxSummaryLength);

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, StructuredOutputOptions, linkedCts.Token)
                .ConfigureAwait(false);

            return ParseAndCap(response.Text, tenantId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordSummarizerFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? "global", _options.TimeoutSeconds);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            _metrics.RecordSummarizerInjection(tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _metrics.RecordSummarizerFailed(tenantId, "transport");
            LogTransportFailure(tenantId ?? "global", ex.Message);
            return null;
        }
    }

    private string? ParseAndCap(string? responseText, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _metrics.RecordSummarizerInjection(tenantId);
            return null;
        }

        SummaryResponse? parsed = JsonSerializer.Deserialize<SummaryResponse>(
            responseText, SerializerOptions);

        if (parsed is null || string.IsNullOrEmpty(parsed.Summary))
        {
            return null;
        }

        if (parsed.Summary.Length <= _options.MaxSummaryLength)
        {
            return parsed.Summary;
        }

        _metrics.RecordSummarizerTruncated(tenantId);
        return parsed.Summary[.._options.MaxSummaryLength];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer throttled for tenant {TenantId}")]
    private partial void LogThrottled(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer timed out for tenant {TenantId} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string tenantId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI summarizer transport failure for tenant {TenantId}: {ErrorMessage}")]
    private partial void LogTransportFailure(string tenantId, string errorMessage);
}
