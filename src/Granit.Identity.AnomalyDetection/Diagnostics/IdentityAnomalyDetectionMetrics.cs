using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Identity.AnomalyDetection.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for session anomaly detection. Meter: <c>Granit.Identity.AnomalyDetection</c>.
/// </summary>
public sealed class IdentityAnomalyDetectionMetrics
{
    /// <summary>The meter name.</summary>
    public const string MeterName = "Granit.Identity.AnomalyDetection";

    private const string TagTenantId = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _assessments;
    private readonly Counter<long> _aiCalls;

    /// <summary>Initializes the metrics from the shared <see cref="IMeterFactory"/>.</summary>
    public IdentityAnomalyDetectionMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _assessments = meter.CreateCounter<long>(
            "granit.identity.session.anomaly.assessments",
            description: "Number of session risk assessments, tagged with the resulting level.");

        _aiCalls = meter.CreateCounter<long>(
            "granit.identity.session.anomaly.ai_calls",
            description: "Number of AI assessment attempts, tagged with the outcome.");
    }

    /// <summary>Records a completed assessment.</summary>
    public void RecordAssessment(string? tenantId, string level, bool aiUsed) =>
        _assessments.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? GlobalTenant },
            { "level", level },
            { "ai_used", aiUsed },
        });

    /// <summary>Records an AI call outcome (<c>succeeded</c>, <c>rate_limited</c>, <c>timeout</c>, or a status).</summary>
    public void RecordAiCall(string? tenantId, string outcome) =>
        _aiCalls.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? GlobalTenant },
            { "outcome", outcome },
        });
}
