using System.Text.RegularExpressions;
using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.AI.Redaction;
using Granit.AI.Sampling;
using Granit.LanguageDetection.AI.Diagnostics;
using Granit.LanguageDetection.AI.Options;
using Granit.LanguageDetection.AI.Prompts;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.LanguageDetection.AI.Internal;

/// <summary>
/// <see cref="ILanguageDetectorProvider"/> backed by a one-shot LLM call via the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). Registered at priority <c>200</c>
/// so it wins over the pure-managed Trigram detector (priority 100) when both are wired into
/// the composite chain.
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful fall-through.</b> Every failure mode — rate-limit denial, timeout, transport
/// error, schema reject, ISO-639-1 mismatch — returns <c>null</c> rather than throwing, so the
/// composite chain falls through to the next provider and the indexing pipeline never blocks.
/// </para>
/// <para>
/// <b>Defence-in-depth against prompt injection (OWASP LLM01).</b> The detector keeps a
/// per-tenant call rate limiter and an optional PII redaction pass; the primitive contributes
/// content isolation (sanitized <c>&lt;data&gt;</c> block) and provider-enforced JSON schema; and
/// an ISO 639-1 regex validates the response. Out-of-schema or out-of-pattern responses bump the
/// <c>granit.language_detection.ai.injections.detected</c> metric (tenant-tagged, NEVER
/// content-tagged) — the rejected payload is dropped on the floor.
/// </para>
/// </remarks>
internal sealed partial class AILanguageDetector : ILanguageDetectorProvider
{
    private const string RateLimitBucketPrefix = "language_detection";

    private readonly IStructuredCompletion _structuredCompletion;
    private readonly IAILanguageDetectionPromptBuilder _promptBuilder;
    private readonly IAICallRateLimiter _rateLimiter;
    private readonly IAIContentRedactor _redactor;
    private readonly LanguageDetectionAIOptions _options;
    private readonly LanguageDetectionAIMetrics _metrics;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AILanguageDetector> _logger;

    public AILanguageDetector(
        IStructuredCompletion structuredCompletion,
        IAILanguageDetectionPromptBuilder promptBuilder,
        IAICallRateLimiter rateLimiter,
        IAIContentRedactor redactor,
        IOptions<LanguageDetectionAIOptions> options,
        LanguageDetectionAIMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<AILanguageDetector> logger)
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
    public int Priority => 200;

    /// <inheritdoc/>
    public async Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length < LanguageDetectorDefaults.MinimumSampleLength)
        {
            return null;
        }

        string? tenantId = _currentTenant is { IsAvailable: true } tenant ? tenant.Id?.ToString() : null;
        string bucketKey = $"{RateLimitBucketPrefix}:{tenantId ?? LanguageDetectionAIMetrics.GlobalTenant}";

        bool admitted = await _rateLimiter
            .TryAcquireAsync(bucketKey, _options.MaxAICallsPerHourPerTenant, cancellationToken)
            .ConfigureAwait(false);

        if (!admitted)
        {
            _metrics.RecordCallThrottled(tenantId);
            LogThrottled(tenantId ?? LanguageDetectionAIMetrics.GlobalTenant);
            return null;
        }

        string sample = AIContentSampler.TruncateOnCodePoint(content, _options.MaxContentLength);

        if (_options.RedactPIIBeforeLLMCall)
        {
            sample = _redactor.Redact(sample);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _metrics.RecordCallAttempted(tenantId);

        var request = new StructuredCompletionRequest
        {
            Instruction = _promptBuilder.BuildInstruction(),
            Content = sample,
            ContentLabel = "Document",
            WorkspaceName = _options.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LanguageDetectionResponse> result = await _structuredCompletion
                .CompleteAsync<LanguageDetectionResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case StructuredCompletionStatus.Succeeded:
                    return ValidateLanguage(result.Value, tenantId);

                case StructuredCompletionStatus.ModelRefused:
                case StructuredCompletionStatus.SchemaViolation:
                    // Empty / out-of-schema output is treated as a rejected (possibly injected) payload.
                    _metrics.RecordInjectionDetected(tenantId);
                    return null;

                default:
                    _metrics.RecordCallFailed(tenantId, "transport");
                    LogTransportFailure(tenantId ?? LanguageDetectionAIMetrics.GlobalTenant, result.Status.ToString());
                    return null;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _metrics.RecordCallFailed(tenantId, "timeout");
            LogTimeout(tenantId ?? LanguageDetectionAIMetrics.GlobalTenant, _options.TimeoutSeconds);
            return null;
        }
    }

    private string? ValidateLanguage(LanguageDetectionResponse? parsed, string? tenantId)
    {
        if (parsed is null || string.IsNullOrEmpty(parsed.Language))
        {
            return null;
        }

        if (!Iso639OneAlpha2().IsMatch(parsed.Language))
        {
            _metrics.RecordInjectionDetected(tenantId);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI language detection transport failure for tenant {TenantId} ({Status})")]
    private partial void LogTransportFailure(string tenantId, string status);
}
