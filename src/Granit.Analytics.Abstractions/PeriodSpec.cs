namespace Granit.Analytics;

/// <summary>
/// A time window — either explicit (<see cref="From"/> / <see cref="To"/>) or named
/// (<see cref="Token"/>) and resolved server-side via <c>TimeProvider</c>.
/// </summary>
/// <param name="From">Inclusive start (only valid when <see cref="Token"/> is null).</param>
/// <param name="To">Exclusive end (only valid when <see cref="Token"/> is null).</param>
/// <param name="Token">
/// Named period (case-insensitive). Supported tokens (v1):
/// <c>today</c>, <c>yesterday</c>, <c>last_7d</c>, <c>last_30d</c>, <c>last_60s</c>,
/// <c>last_5m</c>, <c>mtd</c> (month-to-date), <c>qtd</c> (quarter-to-date),
/// <c>ytd</c> (year-to-date), <c>previous_period</c> (only valid for the comparison
/// window — refers to the period of equal length immediately preceding the main one).
/// </param>
/// <remarks>
/// Lives in <c>Granit.Analytics.Abstractions</c> rather than the HTTP DTOs package so
/// modules outside the analytics HTTP surface (notably <c>Granit.Dashboards.Abstractions</c>
/// for the upcoming <c>DashboardTimeWindow</c> primitive in P1.3) can reuse the same
/// time-window model without taking a dependency on the analytics HTTP layer.
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
