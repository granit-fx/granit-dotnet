using System.Text.Json;
using System.Text.RegularExpressions;
using Granit.AI;
using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.Redaction;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Granit.LanguageDetection.AI.Schema;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.LanguageDetection.AI.Internal;

/// <summary>
/// <see cref="ILanguageDetectorProvider"/> backed by a one-shot LLM call. Registered at
/// priority <c>200</c> so it wins over the pure-managed Trigram detector (priority 100)
/// when both are wired into the composite chain.
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful fall-through.</b> Every failure mode — rate-limit denial, timeout,
/// transport error, schema-reject, ISO-639-1 mismatch — returns <c>null</c> rather
/// than throwing. The composite chain falls through to the next provider so the
/// indexing pipeline never blocks on a stalled or denied LLM.
/// </para>
/// <para>
/// <b>Defence-in-depth against prompt injection (OWASP LLM01).</b> Three layers:
/// instruction-isolation wrapping via <see cref="IAILanguageDetectionPromptBuilder"/>,
/// JSON-schema pinning via <c>ChatResponseFormat.ForJsonSchema</c>, and an ISO 639-1
/// regex validator on the response. Out-of-schema or out-of-pattern responses bump the
/// <c>granit.language_detection.ai.injection_attempt</c> metric (tenant-tagged,
/// NEVER content-tagged) — the rejected payload is dropped on the floor.
/// </para>
/// </remarks>
internal sealed partial class AILanguageDetector : ILanguageDetectorProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly ChatOptions StructuredOutputOptions = new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<LanguageDetectionResponse>(),
    };

    private const int MinimumSampleLength = 10;

    private readonly IAIChatClientFactory _chatClientFactory;
    private readonly IAILanguageDetectionPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly LanguageDetectionAIOptions _options;
    private readonly LanguageDetectionAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AILanguageDetector> _logger;

    public AILanguageDetector(
        IAIChatClientFactory chatClientFactory,
        IAILanguageDetectionPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<LanguageDetectionAIOptions> options,
        LanguageDetectionAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AILanguageDetector> logger)
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
    public int Priority => 200;

    /// <inheritdoc/>
    public async Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length < MinimumSampleLength)
        {
            return null;
        }

        string? tenantId = _currentTenant.Id?.ToString();
        string bucketKey = $"language_detection:{tenantId ?? "global"}";

        bool admitted = await _rateLimiter
            .TryAcquireAsync(bucketKey, _options.MaxAICallsPerHourPerTenant, cancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            _metrics.RecordCallThrottled(tenantId);
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

        _metrics.RecordCallAttempted(tenantId);

        try
        {
            IChatClient chatClient = await _chatClientFactory
                .CreateAsync(_options.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            IReadOnlyList<ChatMessage> messages = _promptBuilder.Build(sample);

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, StructuredOutputOptions, linkedCts.Token)
                .ConfigureAwait(false);

            return ParseAndValidate(response.Text, tenantId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordCallFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? "global", _options.TimeoutSeconds);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            _metrics.RecordInjectionAttempt(tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _metrics.RecordCallFailed(tenantId, "transport");
            LogTransportFailure(tenantId ?? "global", ex.Message);
            return null;
        }
    }

    private string? ParseAndValidate(string? responseText, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            _metrics.RecordInjectionAttempt(tenantId);
            return null;
        }

        LanguageDetectionResponse? parsed = JsonSerializer.Deserialize<LanguageDetectionResponse>(
            responseText, SerializerOptions);

        if (parsed is null || string.IsNullOrEmpty(parsed.Language))
        {
            return null;
        }

        if (!Iso639OneAlpha2().IsMatch(parsed.Language))
        {
            _metrics.RecordInjectionAttempt(tenantId);
            return null;
        }

        return parsed.Language.ToLowerInvariant();
    }

    [GeneratedRegex("^[a-z]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex Iso639OneAlpha2();

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI language detection throttled for tenant {TenantId}")]
    private partial void LogThrottled(string tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI language detection timed out for tenant {TenantId} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string tenantId, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI language detection transport failure for tenant {TenantId}: {ErrorMessage}")]
    private partial void LogTransportFailure(string tenantId, string errorMessage);
}
