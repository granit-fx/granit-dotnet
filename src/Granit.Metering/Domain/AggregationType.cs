namespace Granit.Metering.Domain;

/// <summary>
/// Defines how meter events are aggregated into rollups.
/// </summary>
public enum AggregationType
{
    /// <summary>Sum of all event quantities in the period.</summary>
    Sum = 0,

    /// <summary>Maximum event quantity in the period.</summary>
    Max = 1,

    /// <summary>Count of events in the period (ignores quantity).</summary>
    Count = 2,

    /// <summary>Last recorded event quantity in the period (gauge-style).</summary>
    Last = 3,

    /// <summary>
    /// Distinct count of values extracted from a JSON path in <c>MeterEvent.Metadata</c>
    /// (e.g. <c>"user_id"</c> for monthly active users). Requires
    /// <see cref="MeterDefinition.DistinctProperty"/> to be set; events whose metadata
    /// does not contain the property are excluded from the count.
    /// </summary>
    CountDistinct = 4,
}
