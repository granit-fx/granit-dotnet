using Granit.Analytics;
using Granit.Modularity;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Abstractions.Tests;

/// <summary>
/// Locks the public shape of <see cref="PeriodSpec"/> as it crosses the boundary
/// between the analytics HTTP layer (where it backs <c>MetricRequest.Period</c>) and
/// the dashboards stack (where it will back the upcoming <c>DashboardTimeWindow</c>
/// — story P1.3 follow-up). The record is record-equality only — no behaviour to
/// validate beyond constructibility and the module class hosting the contract.
/// </summary>
public sealed class PeriodSpecTests
{
    [Fact]
    public void Module_IsGranitModule()
        => new GranitAnalyticsAbstractionsModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void DefaultConstruction_LeavesAllFieldsNull()
    {
        PeriodSpec spec = new();

        spec.From.ShouldBeNull();
        spec.To.ShouldBeNull();
        spec.Token.ShouldBeNull();
    }

    [Fact]
    public void TokenForm_CarriesNamedPeriod()
    {
        PeriodSpec spec = new(Token: "mtd");

        spec.Token.ShouldBe("mtd");
        spec.From.ShouldBeNull();
        spec.To.ShouldBeNull();
    }

    [Fact]
    public void AbsoluteForm_CarriesFromAndTo()
    {
        DateTimeOffset from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        PeriodSpec spec = new(From: from, To: to);

        spec.From.ShouldBe(from);
        spec.To.ShouldBe(to);
        spec.Token.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        PeriodSpec a = new(Token: "ytd");
        PeriodSpec b = new(Token: "ytd");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
