using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Analytics.EntityFrameworkCore.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the analytics widget runners — the runtime
/// layer that streams entities through <c>IQueryEngine</c> and shapes them
/// into widget snapshots. Operational counters only; metric *values*
/// themselves travel on the wire response and are not re-emitted as OTel
/// measurements.
/// Meter: <c>Granit.Analytics</c>.
/// </summary>
/// <remarks>
/// Renamed from <c>AnalyticsEndpointsMetrics</c> in D5 (#1569) when the
/// runners moved out of <c>Granit.Analytics.Endpoints</c>. The meter name
/// also changed from <c>Granit.Analytics.Endpoints</c> to
/// <c>Granit.Analytics</c> to align with the new placement; observability
/// dashboards keying off the old name need updating.
/// </remarks>
public sealed class AnalyticsRuntimeMetrics
{
    public const string MeterName = "Granit.Analytics";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _mapInvalidCoordinates;

    public AnalyticsRuntimeMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _mapInvalidCoordinates = meter.CreateCounter<long>(
            "granit.analytics.map.invalid_coordinates",
            description: "Map widget rows skipped because their latitude/longitude failed range or finiteness validation. Bad data does not break the widget — the row is dropped and counted here.");
    }

    /// <summary>
    /// Records a single invalid-coordinate row drop. <paramref name="reason"/> uses
    /// stable snake_case values (<c>"latitude_out_of_range"</c>, <c>"longitude_out_of_range"</c>,
    /// <c>"non_finite"</c>) so dashboards can split the counter without depending on
    /// localised messages.
    /// </summary>
    public void RecordMapInvalidCoordinate(string? tenantId, string reason) =>
        _mapInvalidCoordinates.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });
}
