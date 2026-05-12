using Granit.Timing;

namespace Granit.Dashboards;

/// <summary>
/// Time window applied to every data-bound widget on a dashboard. The frontend
/// surfaces it as a top-of-dashboard control (e.g. "Last 30 days ▾"); user changes
/// propagate to all widgets that don't carry their own <c>TimeWindowOverride</c>.
/// </summary>
/// <param name="Period">
/// The actual period — token-based (<c>last_30d</c>, <c>mtd</c>, <c>ytd</c>, ...) or
/// absolute range. Wraps the framework's existing <see cref="PeriodSpec"/> rather than
/// inventing a parallel time-range model — calendar-aware bounds matter
/// (MTD ≠ "last 30 days") and the same primitive already backs <c>MetricRequest.Period</c>.
/// </param>
/// <param name="Kind">
/// Refresh semantics. <see cref="TimeWindowKind.History"/> = frozen window, refetch on
/// range change. <see cref="TimeWindowKind.Realtime"/> = sliding window, suitable for
/// telemetry-grade widgets with continuous push updates (story P2.4).
/// </param>
/// <param name="CompareTo">
/// Optional comparison window — same model as <c>MetricRequest.CompareTo</c>.
/// Typical value: <c>new PeriodSpec(Token: "previous_period")</c>.
/// </param>
/// <param name="Aggregation">
/// Bucket size for time-series aggregation. When <c>null</c>, the widget's data source
/// picks a sensible default (e.g. day for last-30d, hour for last-24h).
/// </param>
public sealed record DashboardTimeWindow(
    PeriodSpec Period,
    TimeWindowKind Kind = TimeWindowKind.History,
    PeriodSpec? CompareTo = null,
    TimeSpan? Aggregation = null)
{
    /// <summary>Last 24 hours, history kind.</summary>
    public static DashboardTimeWindow Last24Hours => new(PeriodSpec.FromToken("last_24h"));

    /// <summary>Last 7 days, history kind.</summary>
    public static DashboardTimeWindow Last7Days => new(PeriodSpec.FromToken("last_7d"));

    /// <summary>Last 30 days, history kind.</summary>
    public static DashboardTimeWindow Last30Days => new(PeriodSpec.FromToken("last_30d"));

    /// <summary>Month-to-date — calendar window from the 1st of the current month.</summary>
    public static DashboardTimeWindow Mtd => new(PeriodSpec.FromToken("mtd"));

    /// <summary>Year-to-date — calendar window from January 1st of the current year.</summary>
    public static DashboardTimeWindow Ytd => new(PeriodSpec.FromToken("ytd"));

    /// <summary>Last 5 minutes, realtime kind — for live IoT / telemetry widgets.</summary>
    public static DashboardTimeWindow RealtimeLast5Minutes
        => new(PeriodSpec.FromToken("last_5m"), Kind: TimeWindowKind.Realtime);
}

/// <summary>Refresh semantics for a <see cref="DashboardTimeWindow"/>.</summary>
public enum TimeWindowKind
{
    /// <summary>Frozen window — query once per range change. The default.</summary>
    History = 0,

    /// <summary>
    /// Sliding window — refreshes continuously, suitable for telemetry. Pairs with
    /// the SSE subscription transport landing in story P2.4.
    /// </summary>
    Realtime = 1,
}
