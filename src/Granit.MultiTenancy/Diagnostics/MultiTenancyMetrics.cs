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
    private readonly Counter<long> _hostImpersonations;

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

        _hostImpersonations = meter.CreateCounter<long>(
            "granit.multi_tenancy.host_impersonation",
            description: "Host user impersonating a tenant via a non-JWT resolver. Tagged outcome=allowed|denied with deny_reason.");
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

    public void RecordHostImpersonationAllowed(string tenantId) =>
        _hostImpersonations.Add(1, new TagList
        {
            { "outcome", "allowed" },
            { "tenant_id", tenantId },
            { "deny_reason", "none" },
        });

    public void RecordHostImpersonationDenied(string tenantId, string denyReason) =>
        _hostImpersonations.Add(1, new TagList
        {
            { "outcome", "denied" },
            { "tenant_id", tenantId },
            { "deny_reason", denyReason },
        });
}
