using Granit.Analytics;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape of <see cref="DashboardTimeWindow"/> (P1.3): wraps the
/// canonical <see cref="PeriodSpec"/> primitive with an explicit
/// <see cref="TimeWindowKind"/> (History vs Realtime), an optional comparison
/// window, and an optional aggregation bucket. Static presets exist for every
/// common case so consumers don't reach for <c>PeriodSpec.FromToken(...)</c>
/// inline.
/// </summary>
public sealed class DashboardTimeWindowTests
{
    [Fact]
    public void Last30Days_PresetWrapsTokenForm()
    {
        DashboardTimeWindow window = DashboardTimeWindow.Last30Days;

        window.Kind.ShouldBe(TimeWindowKind.History);
        window.Period.Token.ShouldBe("last_30d");
        window.CompareTo.ShouldBeNull();
        window.Aggregation.ShouldBeNull();
    }

    [Fact]
    public void Realtime_PresetCarriesRealtimeKind()
    {
        DashboardTimeWindow window = DashboardTimeWindow.RealtimeLast5Minutes;

        window.Kind.ShouldBe(TimeWindowKind.Realtime);
        window.Period.Token.ShouldBe("last_5m");
    }

    [Fact]
    public void Mtd_Ytd_AreCalendarWindows_NotRollingTimeSpans()
    {
        // Pinned-by-token: the framework's calendar-aware bounds are the whole point
        // of wrapping PeriodSpec rather than a TimeSpan. MTD ≠ "30 days ago".
        DashboardTimeWindow.Mtd.Period.Token.ShouldBe("mtd");
        DashboardTimeWindow.Ytd.Period.Token.ShouldBe("ytd");
    }

    [Fact]
    public void Construction_AcceptsAbsoluteRangeInPeriod()
    {
        DateTimeOffset from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        DashboardTimeWindow window = new(new PeriodSpec(From: from, To: to));

        window.Period.From.ShouldBe(from);
        window.Period.To.ShouldBe(to);
        window.Period.Token.ShouldBeNull();
    }

    [Fact]
    public void CompareTo_AcceptsPreviousPeriodToken()
    {
        DashboardTimeWindow window = new(
            PeriodSpec.FromToken("mtd"),
            CompareTo: PeriodSpec.FromToken("previous_period"));

        window.CompareTo.ShouldNotBeNull();
        window.CompareTo.Token.ShouldBe("previous_period");
    }

    [Fact]
    public void TimeWindowKind_EnumOrderingIsStable()
    {
        // Wire format stability — values land in JSON as integers when no converter is set.
        ((int)TimeWindowKind.History).ShouldBe(0);
        ((int)TimeWindowKind.Realtime).ShouldBe(1);
    }
}
