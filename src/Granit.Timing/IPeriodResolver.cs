namespace Granit.Timing;

/// <summary>
/// Resolves a <see cref="PeriodSpec"/> (explicit bounds or a named calendar token) into an
/// absolute <see cref="ResolvedPeriod"/> — a half-open <c>[From, To)</c> UTC window.
/// </summary>
/// <remarks>
/// The framework-wide implementation resolves calendar tokens in the current user's
/// <em>local</em> wall-clock (via <see cref="ICurrentTimezoneProvider"/> /
/// <see cref="ICurrentFirstDayOfWeekProvider"/>) and converts the bounds back to UTC
/// DST-correctly, so every module (analytics, dashboards, audit, reporting) expands the
/// same token to the same instants.
/// </remarks>
public interface IPeriodResolver
{
    /// <summary>
    /// Resolves the main period to absolute UTC bounds.
    /// </summary>
    /// <param name="spec">The period specification (named token or explicit bounds).</param>
    ResolvedPeriod Resolve(PeriodSpec spec);

    /// <summary>
    /// Resolves the comparison window. Supports the special token <c>previous_period</c>,
    /// which returns the equal-length window immediately preceding <paramref name="main"/>;
    /// any other spec resolves like <see cref="Resolve"/>.
    /// </summary>
    ResolvedPeriod ResolveComparison(PeriodSpec spec, ResolvedPeriod main);
}
