using System.Diagnostics.Metrics;

namespace Granit.Http.Bulkhead;

/// <summary>
/// OpenTelemetry metrics for the bulkhead module.
/// Meter: <c>Granit.Http.Bulkhead</c>.
/// </summary>
public sealed class BulkheadMetrics
{
    public const string MeterName = "Granit.Http.Bulkhead";

    private readonly UpDownCounter<long> _activeCounter;
    private readonly Counter<long> _rejectedCounter;

    public BulkheadMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _activeCounter = meter.CreateUpDownCounter<long>(
            "granit.bulkhead.leases.active",
            description: "Number of currently active bulkhead leases.");
        _rejectedCounter = meter.CreateCounter<long>(
            "granit.bulkhead.requests.rejected",
            description: "Number of requests rejected by bulkhead isolation.");
    }

    public void RecordAcquired(string policyName, string? tenantId) =>
        _activeCounter.Add(1,
            new KeyValuePair<string, object?>("policy", policyName),
            new KeyValuePair<string, object?>("tenant_id", tenantId ?? "global"));

    public void RecordReleased(string policyName, string? tenantId) =>
        _activeCounter.Add(-1,
            new KeyValuePair<string, object?>("policy", policyName),
            new KeyValuePair<string, object?>("tenant_id", tenantId ?? "global"));

    public void RecordRejected(string policyName, string? tenantId) =>
        _rejectedCounter.Add(1,
            new KeyValuePair<string, object?>("policy", policyName),
            new KeyValuePair<string, object?>("tenant_id", tenantId ?? "global"));
}
