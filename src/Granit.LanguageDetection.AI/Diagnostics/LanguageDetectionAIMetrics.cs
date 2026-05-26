using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.LanguageDetection.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the AI language detector. Meter:
/// <c>Granit.LanguageDetection.AI</c>.
/// </summary>
/// <remarks>
/// Counters carry a <c>tenant_id</c> tag coalesced to <see cref="GlobalTenant"/> when no
/// tenant context is active. Content is NEVER tagged — tag cardinality is bounded by the
/// active tenant set, never by user input, so the metrics cannot be turned into a
/// per-request oracle by an attacker.
/// </remarks>
public sealed class LanguageDetectionAIMetrics
{
    public const string MeterName = "Granit.LanguageDetection.AI";

    /// <summary>Fallback tenant tag value when no tenant context is active.</summary>
    internal const string GlobalTenant = "global";

    private readonly Counter<long> _callsAttempted;
    private readonly Counter<long> _callsThrottled;
    private readonly Counter<long> _callsFailed;
    private readonly Counter<long> _injectionsDetected;

    public LanguageDetectionAIMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _callsAttempted = meter.CreateCounter<long>(
            "granit.language_detection.ai.calls.attempted",
            description: "Outbound LLM calls dispatched by the AI language detector.");

        _callsThrottled = meter.CreateCounter<long>(
            "granit.language_detection.ai.calls.throttled",
            description: "Calls skipped because the per-tenant hourly ceiling was exceeded.");

        _callsFailed = meter.CreateCounter<long>(
            "granit.language_detection.ai.calls.failed",
            description: "Calls that did not yield a usable response (timeout, transport, deserialisation).");

        _injectionsDetected = meter.CreateCounter<long>(
            "granit.language_detection.ai.injections.detected",
            description: "Responses rejected because they did not satisfy the JSON schema (potential prompt-injection signal).");
    }

    public void RecordCallAttempted(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? GlobalTenant)];
        _callsAttempted.Add(1, tags);
    }

    public void RecordCallThrottled(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? GlobalTenant)];
        _callsThrottled.Add(1, tags);
    }

    public void RecordCallFailed(string? tenantId, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? GlobalTenant),
            new("reason", reason),
        ];
        _callsFailed.Add(1, tags);
    }

    public void RecordInjectionDetected(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? GlobalTenant)];
        _injectionsDetected.Add(1, tags);
    }
}
