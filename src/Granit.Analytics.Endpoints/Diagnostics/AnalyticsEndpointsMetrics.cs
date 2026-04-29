using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Analytics.Endpoints.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the analytics HTTP endpoints — the runtime layer
/// that turns persisted widget instances into render payloads. Operational
/// counters only; metric *values* themselves travel on the wire response and
/// are not re-emitted as OTel measurements.
/// Meter: <c>Granit.Analytics.Endpoints</c>.
/// </summary>
public sealed class AnalyticsEndpointsMetrics
{
    public const string MeterName = "Granit.Analytics.Endpoints";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _mapInvalidCoordinates;

    public AnalyticsEndpointsMetrics(IMeterFactory meterFactory)
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
