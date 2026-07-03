// =============================================================================
// Tests - PeriodResolver
// =============================================================================
// Verifies the framework period resolver:
//   - UTC parity with the @granit/dashboards client resolver (28 tokens, no tz)
//   - Timezone awareness (calendar tokens resolve the user's LOCAL day)
//   - DST correctness (a "day" spans 23/25h across a transition; bounds stay right)
//   - First-day-of-week (override wins; Monday vs Sunday for wtd/pw)
//   - .NET AddMonths day clamping (May 31 -> last_3mo)
//   - Guard rails (null spec, missing/inverted bounds, unknown token)
//   - previous_period comparison passthrough
// The real Clock + FakeTimeProvider + real ambient providers are used so the
// TimeZoneInfo conversion path is exercised end to end.
// =============================================================================

using System.Globalization;
using Granit.Exceptions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class PeriodResolverTests
{
    // 2026-05-12 14:30:45 UTC — a Tuesday, mid-May, middle of Q2.
    private static readonly DateTimeOffset Now = new(2026, 5, 12, 14, 30, 45, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CurrentTimezoneProvider _timezone = new();
    private readonly CurrentFirstDayOfWeekProvider _firstDayOfWeek = new();
    private readonly PeriodResolver _resolver;

    public PeriodResolverTests()
    {
        _timeProvider.SetUtcNow(Now);
        _resolver = new PeriodResolver(new Clock(_timeProvider, _timezone), _firstDayOfWeek);
    }

    private static DateTimeOffset Utc(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // A fresh resolver pinned to a specific instant/timezone — used by cases whose clock predates
    // the shared field default (FakeTimeProvider refuses to move backwards).
    private static PeriodResolver ResolverAt(DateTimeOffset now, string timezone)
    {
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);
        var timezoneProvider = new CurrentTimezoneProvider { Timezone = timezone };
        return new PeriodResolver(new Clock(timeProvider, timezoneProvider), new CurrentFirstDayOfWeekProvider());
    }

    // -- UTC parity (no timezone) — must equal the client resolver's bounds exactly. -----------

    [Theory]
    // Rolling sub-day windows — [now - duration, now).
    [InlineData("last_60s", "2026-05-12T14:29:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_5m", "2026-05-12T14:25:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_15m", "2026-05-12T14:15:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_30m", "2026-05-12T14:00:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_1h", "2026-05-12T13:30:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_3h", "2026-05-12T11:30:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_6h", "2026-05-12T08:30:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_12h", "2026-05-12T02:30:45Z", "2026-05-12T14:30:45Z")]
    [InlineData("last_24h", "2026-05-11T14:30:45Z", "2026-05-12T14:30:45Z")]
    // Relative single days.
    [InlineData("today", "2026-05-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("yesterday", "2026-05-11T00:00:00Z", "2026-05-12T00:00:00Z")]
    [InlineData("day_before_yesterday", "2026-05-10T00:00:00Z", "2026-05-11T00:00:00Z")]
    [InlineData("this_day_last_week", "2026-05-05T00:00:00Z", "2026-05-06T00:00:00Z")]
    // Rolling day/month/year windows through end of today.
    [InlineData("last_2d", "2026-05-10T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_7d", "2026-05-05T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_30d", "2026-04-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_3mo", "2026-02-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_6mo", "2025-11-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_1y", "2025-05-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_2y", "2024-05-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("last_5y", "2021-05-12T00:00:00Z", "2026-05-13T00:00:00Z")]
    // To-date (default first day = Monday; 05-11 is the Monday of that week).
    [InlineData("wtd", "2026-05-11T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("mtd", "2026-05-01T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("qtd", "2026-04-01T00:00:00Z", "2026-05-13T00:00:00Z")]
    [InlineData("ytd", "2026-01-01T00:00:00Z", "2026-05-13T00:00:00Z")]
    // Previous complete periods.
    [InlineData("pw", "2026-05-04T00:00:00Z", "2026-05-11T00:00:00Z")]
    [InlineData("pm", "2026-04-01T00:00:00Z", "2026-05-01T00:00:00Z")]
    [InlineData("pq", "2026-01-01T00:00:00Z", "2026-04-01T00:00:00Z")]
    [InlineData("py", "2025-01-01T00:00:00Z", "2026-01-01T00:00:00Z")]
    public void Resolve_token_without_timezone_matches_utc_table(string token, string from, string to)
    {
        _firstDayOfWeek.FirstDayOfWeek = DayOfWeek.Monday;

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken(token));

        resolved.From.ShouldBe(Utc(from));
        resolved.To.ShouldBe(Utc(to));
    }

    [Fact]
    public void Resolve_token_is_case_insensitive()
    {
        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("TODAY"));

        resolved.From.ShouldBe(Utc("2026-05-12T00:00:00Z"));
        resolved.To.ShouldBe(Utc("2026-05-13T00:00:00Z"));
    }

    // -- Timezone awareness — calendar tokens resolve the user's LOCAL day. --------------------

    [Fact]
    public void Resolve_today_in_tokyo_returns_local_day_shifted_to_utc()
    {
        // 20:00Z is already 2026-05-13 05:00 in Tokyo (UTC+9): the local day is the 13th.
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero));
        _timezone.Timezone = "Asia/Tokyo";

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("today"));

        // Local midnight 05-13 JST == 05-12 15:00Z; next local midnight == 05-13 15:00Z.
        resolved.From.ShouldBe(Utc("2026-05-12T15:00:00Z"));
        resolved.To.ShouldBe(Utc("2026-05-13T15:00:00Z"));
    }

    [Fact]
    public void Resolve_sub_day_token_is_timezone_independent()
    {
        _timezone.Timezone = "Asia/Tokyo";

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("last_1h"));

        // Sub-day windows are pure UTC instants regardless of timezone.
        resolved.From.ShouldBe(Utc("2026-05-12T13:30:45Z"));
        resolved.To.ShouldBe(Now);
    }

    // -- DST correctness (Europe/Brussels springs forward 2026-03-29 02:00 -> 03:00). ----------

    [Fact]
    public void Resolve_last_2d_across_spring_forward_is_71_hours_not_72()
    {
        PeriodResolver resolver = ResolverAt(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero), "Europe/Brussels");

        ResolvedPeriod resolved = resolver.Resolve(PeriodSpec.FromToken("last_2d"));

        // Local [Mar 28 00:00 CET, Mar 31 00:00 CEST) spans three calendar days, one of which
        // (Mar 29) loses an hour to the DST gap -> 71h total, not a frozen 72h.
        resolved.From.ShouldBe(Utc("2026-03-27T23:00:00Z"));
        resolved.To.ShouldBe(Utc("2026-03-30T22:00:00Z"));
        (resolved.To - resolved.From).ShouldBe(TimeSpan.FromHours(71));
    }

    [Fact]
    public void Resolve_mtd_across_spring_forward_uses_local_month_start()
    {
        PeriodResolver resolver = ResolverAt(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero), "Europe/Brussels");

        ResolvedPeriod resolved = resolver.Resolve(PeriodSpec.FromToken("mtd"));

        // Mar 1 00:00 local is still CET (+1) -> Feb 28 23:00Z; end of today is CEST (+2).
        resolved.From.ShouldBe(Utc("2026-02-28T23:00:00Z"));
        resolved.To.ShouldBe(Utc("2026-03-30T22:00:00Z"));
    }

    // -- First day of week. --------------------------------------------------------------------

    [Theory]
    [InlineData(DayOfWeek.Monday, "2026-05-11T00:00:00Z")] // Mon of the week containing Tue 05-12
    [InlineData(DayOfWeek.Sunday, "2026-05-10T00:00:00Z")] // Sun of that week
    public void Resolve_wtd_honours_first_day_of_week(DayOfWeek firstDay, string expectedFrom)
    {
        _firstDayOfWeek.FirstDayOfWeek = firstDay;

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("wtd"));

        resolved.From.ShouldBe(Utc(expectedFrom));
        resolved.To.ShouldBe(Utc("2026-05-13T00:00:00Z"));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, "2026-05-04T00:00:00Z", "2026-05-11T00:00:00Z")]
    [InlineData(DayOfWeek.Sunday, "2026-05-03T00:00:00Z", "2026-05-10T00:00:00Z")]
    public void Resolve_pw_honours_first_day_of_week(DayOfWeek firstDay, string from, string to)
    {
        _firstDayOfWeek.FirstDayOfWeek = firstDay;

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("pw"));

        resolved.From.ShouldBe(Utc(from));
        resolved.To.ShouldBe(Utc(to));
    }

    [Fact]
    public void Resolve_wtd_without_override_falls_back_to_current_culture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // en-US: first day is Sunday. No explicit override -> derive from culture.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            _firstDayOfWeek.FirstDayOfWeek = null;

            ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("wtd"));

            resolved.From.ShouldBe(Utc("2026-05-10T00:00:00Z")); // Sunday
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Resolve_first_day_override_wins_over_culture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); // culture says Sunday
            _firstDayOfWeek.FirstDayOfWeek = DayOfWeek.Monday; // override says Monday

            ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("wtd"));

            resolved.From.ShouldBe(Utc("2026-05-11T00:00:00Z")); // Monday wins
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // -- AddMonths day clamping. ---------------------------------------------------------------

    [Fact]
    public void Resolve_last_3mo_clamps_day_to_target_month()
    {
        // day0 = May 31 -> AddMonths(-3) = Feb 31 -> clamped to Feb 28 (2026 is not a leap year).
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));

        ResolvedPeriod resolved = _resolver.Resolve(PeriodSpec.FromToken("last_3mo"));

        resolved.From.ShouldBe(Utc("2026-02-28T00:00:00Z"));
        resolved.To.ShouldBe(Utc("2026-06-01T00:00:00Z"));
    }

    // -- Explicit bounds & guard rails. --------------------------------------------------------

    [Fact]
    public void Resolve_explicit_window_passes_through()
    {
        var spec = new PeriodSpec(From: Now.AddDays(-3), To: Now);

        ResolvedPeriod resolved = _resolver.Resolve(spec);

        resolved.From.ShouldBe(Now.AddDays(-3));
        resolved.To.ShouldBe(Now);
    }

    [Fact]
    public void Resolve_null_spec_throws() =>
        Should.Throw<ArgumentNullException>(() => _resolver.Resolve(null!));

    [Fact]
    public void Resolve_missing_bounds_throws_business_rule()
    {
        var spec = new PeriodSpec(From: Now, To: null);

        BusinessRuleViolationException ex =
            Should.Throw<BusinessRuleViolationException>(() => _resolver.Resolve(spec));
        ex.ErrorCode.ShouldBe("Granit.Timing:PeriodSpecBounds");
    }

    [Fact]
    public void Resolve_inverted_bounds_throws_business_rule()
    {
        var spec = new PeriodSpec(From: Now, To: Now.AddDays(-1));

        BusinessRuleViolationException ex =
            Should.Throw<BusinessRuleViolationException>(() => _resolver.Resolve(spec));
        ex.ErrorCode.ShouldBe("Granit.Timing:PeriodSpecOrdering");
    }

    [Fact]
    public void Resolve_unknown_token_throws_business_rule()
    {
        BusinessRuleViolationException ex =
            Should.Throw<BusinessRuleViolationException>(() => _resolver.Resolve(PeriodSpec.FromToken("last_decade")));

        ex.ErrorCode.ShouldBe("Granit.Timing:UnknownPeriodToken");
        ex.Message.ShouldContain("last_decade");
    }

    // -- Comparison window. --------------------------------------------------------------------

    [Fact]
    public void ResolveComparison_previous_period_returns_equal_length_window_before_main()
    {
        var main = new ResolvedPeriod(Utc("2026-05-05T00:00:00Z"), Utc("2026-05-12T00:00:00Z"));

        ResolvedPeriod comparison = _resolver.ResolveComparison(PeriodSpec.FromToken("previous_period"), main);

        comparison.From.ShouldBe(Utc("2026-04-28T00:00:00Z"));
        comparison.To.ShouldBe(Utc("2026-05-05T00:00:00Z"));
    }

    [Fact]
    public void ResolveComparison_non_previous_period_resolves_like_main()
    {
        var main = new ResolvedPeriod(Now.AddDays(-1), Now);
        _firstDayOfWeek.FirstDayOfWeek = DayOfWeek.Monday;

        ResolvedPeriod comparison = _resolver.ResolveComparison(PeriodSpec.FromToken("yesterday"), main);

        comparison.From.ShouldBe(Utc("2026-05-11T00:00:00Z"));
        comparison.To.ShouldBe(Utc("2026-05-12T00:00:00Z"));
    }
}
