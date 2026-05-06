using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.MultiTenancy.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the multi-tenancy module.
/// Meter: <c>Granit.MultiTenancy</c>.
/// </summary>
public sealed class MultiTenancyMetrics
{
    public const string MeterName = "Granit.MultiTenancy";

    private readonly Counter<long> _resolutionsSucceeded;
    private readonly Counter<long> _resolutionsFailed;
    private readonly Counter<long> _contextSwitches;
    private readonly Counter<long> _membershipRejected;

    public MultiTenancyMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _resolutionsSucceeded = meter.CreateCounter<long>(
            "granit.multi_tenancy.resolution.succeeded",
            description: "Number of successful tenant resolutions.");

        _resolutionsFailed = meter.CreateCounter<long>(
            "granit.multi_tenancy.resolution.failed",
            description: "Number of failed tenant resolutions (no resolver matched).");

        _contextSwitches = meter.CreateCounter<long>(
            "granit.multi_tenancy.context.switched",
            description: "Number of explicit tenant context switches.");

        _membershipRejected = meter.CreateCounter<long>(
            "granit.multi_tenancy.membership.rejected",
            description:
                "Number of requests rejected by the membership check "
                + "(authenticated user is not a member of the resolved tenant). "
                + "Anomalous volume signals tenant-claim spoofing attempts.");
    }

    public void RecordResolutionSucceeded(string tenantId, string resolverType) =>
        _resolutionsSucceeded.Add(1, new TagList
        {
            { "tenant_id", tenantId },
            { "resolver_type", resolverType },
        });

    public void RecordResolutionFailed() =>
        _resolutionsFailed.Add(1, new TagList
        {
            { "tenant_id", "global" },
        });

    public void RecordContextSwitched(string? tenantId) =>
        _contextSwitches.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordMembershipRejected() =>
        _membershipRejected.Add(1);
}
