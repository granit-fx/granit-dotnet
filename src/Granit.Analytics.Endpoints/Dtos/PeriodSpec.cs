namespace Granit.Analytics.Endpoints.Dtos;

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
public sealed record PeriodSpec(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Token = null);
