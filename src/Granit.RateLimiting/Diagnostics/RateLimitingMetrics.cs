using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.RateLimiting.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the rate limiting module.
/// Meter: <c>Granit.RateLimiting</c>.
/// </summary>
public sealed class RateLimitingMetrics
{
    public const string MeterName = "Granit.RateLimiting";

    private readonly Counter<long> _allowedCounter;
    private readonly Counter<long> _rejectedCounter;

    public RateLimitingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _allowedCounter = meter.CreateCounter<long>(
            "granit.rate_limiting.requests.allowed",
            description: "Number of requests allowed by rate limiting.");
        _rejectedCounter = meter.CreateCounter<long>(
            "granit.rate_limiting.requests.rejected",
            description: "Number of requests rejected by rate limiting.");
    }

    public void RecordAllowed(string policyName, string? tenantId) =>
        _allowedCounter.Add(1, new TagList
        {
            { "policy", policyName },
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordRejected(string policyName, string? tenantId) =>
        _rejectedCounter.Add(1, new TagList
        {
            { "policy", policyName },
            { "tenant_id", tenantId ?? "global" },
        });
}
