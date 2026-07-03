namespace Granit.Timing;

/// <summary>
/// A time window — either explicit (<see cref="From"/> / <see cref="To"/>) or named
/// (<see cref="Token"/>) and resolved server-side via <c>TimeProvider</c>.
/// </summary>
/// <param name="From">Inclusive start (only valid when <see cref="Token"/> is null).</param>
/// <param name="To">Exclusive end (only valid when <see cref="Token"/> is null).</param>
/// <param name="Token">
/// Named period (case-insensitive), resolved by <see cref="IPeriodResolver"/>. Supported:
/// <list type="bullet">
/// <item>Rolling sub-day (UTC, timezone-independent): <c>last_60s</c>, <c>last_5m</c>,
/// <c>last_15m</c>, <c>last_30m</c>, <c>last_1h</c>, <c>last_3h</c>, <c>last_6h</c>,
/// <c>last_12h</c>, <c>last_24h</c>.</item>
/// <item>Relative days (local, day-aligned): <c>today</c>, <c>yesterday</c>,
/// <c>day_before_yesterday</c>, <c>this_day_last_week</c>.</item>
/// <item>Rolling day/month/year: <c>last_2d</c>, <c>last_7d</c>, <c>last_30d</c>,
/// <c>last_3mo</c>, <c>last_6mo</c>, <c>last_1y</c>, <c>last_2y</c>, <c>last_5y</c>.</item>
/// <item>To-date: <c>wtd</c>, <c>mtd</c>, <c>qtd</c>, <c>ytd</c>.</item>
/// <item>Previous complete periods: <c>pw</c>, <c>pm</c>, <c>pq</c>, <c>py</c>.</item>
/// <item><c>previous_period</c> — only valid for the comparison window; refers to the period
/// of equal length immediately preceding the main one.</item>
/// </list>
/// </param>
/// <remarks>
/// Lives in <c>Granit.Timing</c> so any module needing a time-window primitive
/// (analytics, dashboards, IoT, audit, reporting) can reuse it without taking a
/// dependency on a domain-specific package.
/// </remarks>
public sealed record PeriodSpec(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Token = null)
{
    /// <summary>
    /// Convenience factory for the named-token form. Equivalent to
    /// <c>new PeriodSpec(Token: token)</c>; preferred at call sites because the
    /// <c>Token:</c> named argument pattern is flagged by the framework's
    /// hardcoded-secret analyzer (the literal "token" identifier matches the
    /// security heuristic, false positive for time-window tokens).
    /// </summary>
    public static PeriodSpec FromToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new PeriodSpec(From: null, To: null, Token: token);
    }
}
