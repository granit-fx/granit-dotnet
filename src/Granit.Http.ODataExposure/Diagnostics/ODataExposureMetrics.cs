using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.ODataExposure.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the OData exposure layer. Tracks request-time
/// hardening events surfaced by C3 (#1392): rejected queries (count
/// disabled, expand not whitelisted) and silent clamps (top too high).
/// Meter: <c>Granit.Http.ODataExposure</c>.
/// </summary>
public sealed class ODataExposureMetrics
{
    public const string MeterName = "Granit.Http.ODataExposure";

    private const string TagTenantId = "tenant_id";
    private const string TagEntitySet = "entity_set";
    private const string TagReason = "reason";
    private const string TagFeedKind = "feed_kind";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _rejectedQueries;
    private readonly Counter<long> _topClamped;

    public ODataExposureMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _rejectedQueries = meter.CreateCounter<long>(
            "granit.odata.query.rejected",
            description: "OData queries rejected at the per-EntitySet hardening layer ($count disabled, $expand not whitelisted, etc.). Tagged with the rejection reason.");

        _topClamped = meter.CreateCounter<long>(
            "granit.odata.query.top_clamped",
            description: "OData queries whose user-supplied $top exceeded the EntitySet's MaxTop and was silently clamped. The OData-MaxTop-Applied response header surfaces the same event.");
    }

    /// <summary>Records a rejection at the C3 hardening layer.</summary>
    /// <param name="entitySet">EntitySet name surfaced on the route (e.g. <c>"Invoices"</c>).</param>
    /// <param name="reason">Stable snake_case reason tag (<c>"count_disabled"</c>, <c>"expand_not_whitelisted"</c>).</param>
    /// <param name="tenantId">Resolved tenant id, or <see langword="null"/> when no tenant is active. Coalesced to <c>"global"</c> on the metric.</param>
    /// <param name="feedKind">Feed origin (<c>"tenant"</c> or <c>"host"</c>) — distinguishes per-tenant exposure events from cross-tenant host-feed events for audit dashboards.</param>
    public void RecordRejectedQuery(string entitySet, string reason, string? tenantId, string feedKind) =>
        _rejectedQueries.Add(1, new TagList
        {
            { TagEntitySet, entitySet },
            { TagReason, reason },
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagFeedKind, feedKind },
        });

    /// <summary>Records a $top clamp.</summary>
    /// <param name="entitySet">EntitySet name surfaced on the route.</param>
    /// <param name="tenantId">Resolved tenant id, or <see langword="null"/> when no tenant is active. Coalesced to <c>"global"</c> on the metric.</param>
    /// <param name="feedKind">Feed origin (<c>"tenant"</c> or <c>"host"</c>).</param>
    public void RecordTopClamped(string entitySet, string? tenantId, string feedKind) =>
        _topClamped.Add(1, new TagList
        {
            { TagEntitySet, entitySet },
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagFeedKind, feedKind },
        });
}
