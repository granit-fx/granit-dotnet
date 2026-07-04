using System.Globalization;
using Granit.Exceptions;

namespace Granit.Timing;

/// <summary>
/// Framework implementation of <see cref="IPeriodResolver"/>. Resolves named calendar tokens
/// in the current user's local wall-clock and converts the bounds back to UTC DST-correctly,
/// using <see cref="IClock"/> as the sole time source (never <c>DateTimeOffset.UtcNow</c>).
/// </summary>
/// <remarks>
/// <para>
/// Sub-day rolling tokens (<c>last_5m</c>, <c>last_24h</c>, …) are timezone-independent — they
/// resolve to <c>[now - duration, now)</c> in UTC. Calendar tokens (<c>today</c>, <c>wtd</c>,
/// <c>mtd</c>, …) are day-aligned in the user's local time: their bounds are built as local
/// wall-clock instants and translated to UTC via <see cref="IClock.ToUtcFromUserLocal"/>, so a
/// "day" correctly spans 23/24/25 hours across a DST transition.
/// </para>
/// <para>
/// With no configured timezone the user operates in UTC and the bounds match the client-side
/// resolver (<c>@granit/dashboards</c> <c>resolveTimeWindowToRenderRequest</c>) exactly.
/// </para>
/// </remarks>
internal sealed class PeriodResolver(IClock clock, ICurrentFirstDayOfWeekProvider firstDayOfWeekProvider)
    : IPeriodResolver
{
    private const string SupportedTokens =
        "today, yesterday, day_before_yesterday, this_day_last_week, last_60s, last_5m, last_15m, "
        + "last_30m, last_1h, last_3h, last_6h, last_12h, last_24h, last_2d, last_7d, last_30d, "
        + "last_3mo, last_6mo, last_1y, last_2y, last_5y, wtd, mtd, qtd, ytd, pw, pm, pq, py.";

    private readonly IClock _clock = clock;
    private readonly ICurrentFirstDayOfWeekProvider _firstDayOfWeekProvider = firstDayOfWeekProvider;

    /// <inheritdoc />
    public ResolvedPeriod Resolve(PeriodSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!string.IsNullOrWhiteSpace(spec.Token))
        {
            return ResolveToken(spec.Token, _clock.Now, EffectiveFirstDayOfWeek);
        }

        // Bounds validation is normally enforced upstream by the request validator; these
        // guards are defence-in-depth for callers that bypass it (internal jobs, tests).
        if (spec.From is null || spec.To is null)
        {
            throw new BusinessRuleViolationException(
                "Granit.Timing:PeriodSpecBounds",
                "PeriodSpec requires either Token or both From and To.");
        }

        if (spec.From >= spec.To)
        {
            throw new BusinessRuleViolationException(
                "Granit.Timing:PeriodSpecOrdering",
                "PeriodSpec.From must be strictly before PeriodSpec.To.");
        }

        return new ResolvedPeriod(spec.From.Value, spec.To.Value);
    }

    /// <inheritdoc />
    public ResolvedPeriod ResolveComparison(PeriodSpec spec, ResolvedPeriod main)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (string.Equals(spec.Token, "previous_period", StringComparison.OrdinalIgnoreCase))
        {
            TimeSpan length = main.To - main.From;
            return new ResolvedPeriod(main.From - length, main.From);
        }

        return Resolve(spec);
    }

    /// <summary>
    /// Effective first day of the week: the explicit user preference, or the current culture's
    /// default when unset. The culture is hydrated from the user's preferred locale upstream,
    /// so this fallback already reflects the user's preference.
    /// </summary>
    private DayOfWeek EffectiveFirstDayOfWeek =>
        _firstDayOfWeekProvider.FirstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    private ResolvedPeriod ResolveToken(string rawToken, DateTimeOffset nowUtc, DayOfWeek w)
    {
        string token = rawToken.ToLowerInvariant();

        // Current instant expressed in the user's local wall-clock, then day-aligned.
        DateTime day0 = _clock.ConvertToUserTime(nowUtc).DateTime.Date;
        DateTime endOfToday = day0.AddDays(1);

        return token switch
        {
            // Rolling sub-day windows — timezone-independent, end at "now".
            "last_60s" => Utc(nowUtc.AddSeconds(-60), nowUtc),
            "last_5m" => Utc(nowUtc.AddMinutes(-5), nowUtc),
            "last_15m" => Utc(nowUtc.AddMinutes(-15), nowUtc),
            "last_30m" => Utc(nowUtc.AddMinutes(-30), nowUtc),
            "last_1h" => Utc(nowUtc.AddHours(-1), nowUtc),
            "last_3h" => Utc(nowUtc.AddHours(-3), nowUtc),
            "last_6h" => Utc(nowUtc.AddHours(-6), nowUtc),
            "last_12h" => Utc(nowUtc.AddHours(-12), nowUtc),
            "last_24h" => Utc(nowUtc.AddHours(-24), nowUtc),

            // Relative single days — day-aligned (local).
            "today" => Local(day0, endOfToday),
            "yesterday" => Local(day0.AddDays(-1), day0),
            "day_before_yesterday" => Local(day0.AddDays(-2), day0.AddDays(-1)),
            "this_day_last_week" => Local(day0.AddDays(-7), day0.AddDays(-6)),

            // Rolling day/month/year windows — day-aligned, through end of today.
            "last_2d" => Local(day0.AddDays(-2), endOfToday),
            "last_7d" => Local(day0.AddDays(-7), endOfToday),
            "last_30d" => Local(day0.AddDays(-30), endOfToday),
            "last_3mo" => Local(day0.AddMonths(-3), endOfToday),
            "last_6mo" => Local(day0.AddMonths(-6), endOfToday),
            "last_1y" => Local(day0.AddYears(-1), endOfToday),
            "last_2y" => Local(day0.AddYears(-2), endOfToday),
            "last_5y" => Local(day0.AddYears(-5), endOfToday),

            // To-date ("so far") — start of the current period through end of today.
            "wtd" => Local(WeekStart(day0, w), endOfToday),
            "mtd" => Local(FirstOfMonth(day0), endOfToday),
            "qtd" => Local(QuarterStart(day0), endOfToday),
            "ytd" => Local(FirstOfYear(day0), endOfToday),

            // Previous complete periods.
            "pw" => Local(WeekStart(day0, w).AddDays(-7), WeekStart(day0, w)),
            "pm" => Local(FirstOfMonth(day0).AddMonths(-1), FirstOfMonth(day0)),
            "pq" => Local(QuarterStart(day0).AddMonths(-3), QuarterStart(day0)),
            "py" => Local(FirstOfYear(day0).AddYears(-1), FirstOfYear(day0)),

            _ => throw new BusinessRuleViolationException(
                "Granit.Timing:UnknownPeriodToken",
                $"Unknown period token '{rawToken}'. Supported: {SupportedTokens}"),
        };
    }

    /// <summary>Builds a UTC window from two instants that are already absolute.</summary>
    private static ResolvedPeriod Utc(DateTimeOffset from, DateTimeOffset to) => new(from, to);

    /// <summary>Builds a window from two local wall-clock bounds, converting each to UTC DST-correctly.</summary>
    private ResolvedPeriod Local(DateTime from, DateTime to) =>
        new(_clock.ToUtcFromUserLocal(from), _clock.ToUtcFromUserLocal(to));

    /// <summary>Most recent day <c>&lt;= <paramref name="d"/></c> whose weekday equals <paramref name="w"/>.</summary>
    private static DateTime WeekStart(DateTime d, DayOfWeek w)
    {
        int diff = ((int)d.DayOfWeek - (int)w + 7) % 7;
        return d.AddDays(-diff);
    }

    private static DateTime FirstOfMonth(DateTime d) => new(d.Year, d.Month, 1, 0, 0, 0, d.Kind);

    private static DateTime FirstOfYear(DateTime d) => new(d.Year, 1, 1, 0, 0, 0, d.Kind);

    private static DateTime QuarterStart(DateTime d)
    {
        int quarterStartMonth = ((d.Month - 1) / 3 * 3) + 1;
        return new DateTime(d.Year, quarterStartMonth, 1, 0, 0, 0, d.Kind);
    }
}
