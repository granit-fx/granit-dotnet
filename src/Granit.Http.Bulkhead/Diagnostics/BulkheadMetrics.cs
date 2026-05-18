using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.Bulkhead.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the bulkhead module.
/// Meter: <c>Granit.Http.Bulkhead</c>.
/// </summary>
public sealed class BulkheadMetrics
{
    /// <summary>Name of the OpenTelemetry meter emitted by this module.</summary>
    public const string MeterName = "Granit.Http.Bulkhead";

    private const string PolicyTag = "policy";
    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantId = "global";

    private readonly UpDownCounter<long> _activeCounter;
    private readonly Counter<long> _rejectedCounter;
    private readonly Counter<long> _evictedCounter;
    private readonly Counter<long> _bypassedCounter;
    private readonly Counter<long> _unknownPolicyCounter;
    private readonly Counter<long> _abandonedCounter;

    /// <summary>Initializes the meter and counters via <see cref="IMeterFactory"/>.</summary>
    public BulkheadMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _activeCounter = meter.CreateUpDownCounter<long>(
            "granit.http.bulkhead.leases.active",
            description: "Number of currently active bulkhead leases.");
        _rejectedCounter = meter.CreateCounter<long>(
            "granit.http.bulkhead.requests.rejected",
            description: "Number of requests rejected by bulkhead isolation.");
        _evictedCounter = meter.CreateCounter<long>(
            "granit.http.bulkhead.limiters.evicted",
            description: "Number of concurrency limiters evicted from the registry (idle sweep or LRU pressure).");
        _bypassedCounter = meter.CreateCounter<long>(
            "granit.http.bulkhead.requests.bypassed",
            description: "Number of acquire calls bypassed (machine actor or BypassRoles match).");
        _unknownPolicyCounter = meter.CreateCounter<long>(
            "granit.http.bulkhead.policy.unknown",
            description: "Number of acquire calls referencing a policy name not present in configuration.");
        _abandonedCounter = meter.CreateCounter<long>(
            "granit.http.bulkhead.requests.abandoned",
            description: "Number of acquire calls abandoned by the caller (client cancel) while waiting for a permit.");
    }

    /// <summary>Records that a permit was acquired for <paramref name="policyName"/> and <paramref name="tenantId"/>.</summary>
    public void RecordAcquired(string policyName, string? tenantId) =>
        _activeCounter.Add(1, new TagList
        {
            { PolicyTag, policyName },
            { TenantIdTag, tenantId ?? GlobalTenantId },
        });

    /// <summary>Records that a permit was released for <paramref name="policyName"/> and <paramref name="tenantId"/>.</summary>
    public void RecordReleased(string policyName, string? tenantId) =>
        _activeCounter.Add(-1, new TagList
        {
            { PolicyTag, policyName },
            { TenantIdTag, tenantId ?? GlobalTenantId },
        });

    /// <summary>Records that an acquire attempt was rejected for <paramref name="policyName"/> and <paramref name="tenantId"/>.</summary>
    public void RecordRejected(string policyName, string? tenantId) =>
        _rejectedCounter.Add(1, new TagList
        {
            { PolicyTag, policyName },
            { TenantIdTag, tenantId ?? GlobalTenantId },
        });

    /// <summary>
    /// Records that <paramref name="count"/> limiters were evicted by the given <paramref name="reason"/>
    /// (<c>idle</c> for the background sweep, <c>lru</c> for capacity-driven eviction).
    /// </summary>
    public void RecordEvicted(string reason, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _evictedCounter.Add(count, new TagList { { "reason", reason } });
    }

    /// <summary>Records that an acquire was bypassed for <paramref name="policyName"/> with <paramref name="reason"/> (<c>machine</c> or <c>role</c>).</summary>
    public void RecordBypassed(string policyName, string reason) =>
        _bypassedCounter.Add(1, new TagList
        {
            { PolicyTag, policyName },
            { "reason", reason },
        });

    /// <summary>Records a reference to a policy name not present in configuration.</summary>
    public void RecordUnknownPolicy(string policyName) =>
        _unknownPolicyCounter.Add(1, new TagList { { PolicyTag, policyName } });

    /// <summary>Records that an acquire call was abandoned by the caller (client cancel) before acquiring a permit.</summary>
    public void RecordAbandoned(string policyName, string? tenantId) =>
        _abandonedCounter.Add(1, new TagList
        {
            { PolicyTag, policyName },
            { TenantIdTag, tenantId ?? GlobalTenantId },
        });
}
