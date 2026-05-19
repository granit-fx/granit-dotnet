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

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

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
            "granit.multi_tenancy.host_impersonation.attempted",
            description: "Host user attempting to impersonate a tenant via a non-JWT resolver. Tagged outcome=allowed|denied with deny_reason.");
    }

    public void RecordResolutionSucceeded(string tenantId, string resolverType) =>
        _resolutionsSucceeded.Add(1, new TagList
        {
            { TenantIdTag, tenantId },
            { "resolver_type", resolverType },
        });

    public void RecordResolutionFailed() =>
        _resolutionsFailed.Add(1, new TagList
        {
            { TenantIdTag, GlobalTenant },
        });

    public void RecordContextSwitched(string? tenantId) =>
        _contextSwitches.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
        });

    public void RecordHostImpersonationAllowed(string tenantId) =>
        _hostImpersonations.Add(1, new TagList
        {
            { "outcome", "allowed" },
            { TenantIdTag, tenantId },
            { "deny_reason", "none" },
        });

    public void RecordHostImpersonationDenied(string tenantId, string denyReason) =>
        _hostImpersonations.Add(1, new TagList
        {
            { "outcome", "denied" },
            { TenantIdTag, tenantId },
            { "deny_reason", denyReason },
        });
}
