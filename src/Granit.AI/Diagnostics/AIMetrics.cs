using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.AI.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit AI module.
/// Meter: <c>Granit.AI</c>.
/// </summary>
public sealed class AIMetrics
{
    public const string MeterName = "Granit.AI";

    private readonly Counter<long> _requestsCompleted;
    private readonly Counter<long> _tokensInput;
    private readonly Counter<long> _tokensOutput;
    private readonly Histogram<double> _requestDuration;

    public AIMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _requestsCompleted = meter.CreateCounter<long>(
            "granit.ai.requests.completed",
            description: "Number of AI requests completed.");

        _tokensInput = meter.CreateCounter<long>(
            "granit.ai.tokens.input",
            description: "Number of input tokens consumed.");

        _tokensOutput = meter.CreateCounter<long>(
            "granit.ai.tokens.output",
            description: "Number of output tokens produced.");

        _requestDuration = meter.CreateHistogram<double>(
            "granit.ai.request.duration",
            unit: "s",
            description: "Duration of AI requests in seconds.");
    }

    public void RecordRequestCompleted(string? tenantId, string model, string provider, string status) =>
        _requestsCompleted.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "model", model },
            { "provider", provider },
            { "status", status },
        });

    public void RecordTokensUsed(string? tenantId, string model, string provider, long inputTokens, long outputTokens)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "model", model },
            { "provider", provider },
        };

        _tokensInput.Add(inputTokens, tags);
        _tokensOutput.Add(outputTokens, tags);
    }

    public void RecordRequestDuration(string? tenantId, string model, string provider, TimeSpan duration) =>
        _requestDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "model", model },
            { "provider", provider },
        });
}
