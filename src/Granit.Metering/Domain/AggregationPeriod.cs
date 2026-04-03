namespace Granit.Metering.Domain;

/// <summary>
/// The time granularity for usage aggregation rollups.
/// </summary>
public enum AggregationPeriod
{
    /// <summary>Hourly rollup.</summary>
    Hourly = 0,

    /// <summary>Daily rollup.</summary>
    Daily = 1,

    /// <summary>Full billing-period rollup (aligned with subscription cycle).</summary>
    BillingPeriod = 2,
}
