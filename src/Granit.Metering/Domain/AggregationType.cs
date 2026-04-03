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
}
