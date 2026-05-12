namespace Granit.Timing;

/// <summary>
/// An absolute, half-open <c>[From, To)</c> time window — the resolved form of a
/// <see cref="PeriodSpec"/> after named-token expansion against the framework's
/// <c>IClock</c>. Public so it can travel across module boundaries (analytics,
/// dashboards, IoT, audit) without forcing every consumer to re-resolve the same
/// token in different code paths.
/// </summary>
/// <param name="From">Inclusive lower bound (UTC).</param>
/// <param name="To">Exclusive upper bound (UTC).</param>
public readonly record struct ResolvedPeriod(DateTimeOffset From, DateTimeOffset To);
