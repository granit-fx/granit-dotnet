namespace Granit.QueryEngine;

/// <summary>
/// Predefined date periods for date filter shortcuts.
/// </summary>
public enum DatePeriod
{
    /// <summary>Today only.</summary>
    Today,

    /// <summary>Current week (Monday to Sunday).</summary>
    ThisWeek,

    /// <summary>Current month.</summary>
    ThisMonth,

    /// <summary>Previous month.</summary>
    LastMonth,

    /// <summary>Current quarter.</summary>
    ThisQuarter,

    /// <summary>Current year.</summary>
    ThisYear,

    /// <summary>Custom date range (user-specified start/end).</summary>
    Custom,
}
