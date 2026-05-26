using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Indexing.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the AI summarizer (and future AI auto-tagger). Meter:
/// <c>Granit.Indexing.AI</c>.
/// </summary>
/// <remarks>
/// Counters carry a <c>tenant_id</c> tag coalesced to <c>"global"</c> when no tenant
/// context is active. Content is NEVER tagged — tag cardinality is bounded by the
/// active tenant set, never by user input.
/// </remarks>
public sealed class IndexingAIMetrics
{
    public const string MeterName = "Granit.Indexing.AI";

    private readonly Counter<long> _summarizerAttempted;
    private readonly Counter<long> _summarizerThrottled;
    private readonly Counter<long> _summarizerFailed;
    private readonly Counter<long> _summarizerTruncated;
    private readonly Counter<long> _summarizerInjection;

    public IndexingAIMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _summarizerAttempted = meter.CreateCounter<long>(
            "granit.indexing.ai.summarizer.calls.attempted",
            description: "Outbound LLM calls dispatched by the AI summarizer.");

        _summarizerThrottled = meter.CreateCounter<long>(
            "granit.indexing.ai.summarizer.calls.throttled",
            description: "Summarizer calls skipped because the per-tenant hourly ceiling was exceeded.");

        _summarizerFailed = meter.CreateCounter<long>(
            "granit.indexing.ai.summarizer.calls.failed",
            description: "Summarizer calls that did not yield a usable response (timeout, transport, deserialisation).");

        _summarizerTruncated = meter.CreateCounter<long>(
            "granit.indexing.ai.summarizer.truncated",
            description: "Summarizer responses truncated because they exceeded the configured length cap.");

        _summarizerInjection = meter.CreateCounter<long>(
            "granit.indexing.ai.summarizer.injection_attempt",
            description: "Summarizer responses rejected because they did not satisfy the JSON schema (potential prompt-injection signal).");
    }

    public void RecordSummarizerAttempted(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? "global")];
        _summarizerAttempted.Add(1, tags);
    }

    public void RecordSummarizerThrottled(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? "global")];
        _summarizerThrottled.Add(1, tags);
    }

    public void RecordSummarizerFailed(string? tenantId, string reason)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("reason", reason),
        ];
        _summarizerFailed.Add(1, tags);
    }

    public void RecordSummarizerTruncated(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? "global")];
        _summarizerTruncated.Add(1, tags);
    }

    public void RecordSummarizerInjection(string? tenantId)
    {
        TagList tags = [new("tenant_id", tenantId ?? "global")];
        _summarizerInjection.Add(1, tags);
    }
}
