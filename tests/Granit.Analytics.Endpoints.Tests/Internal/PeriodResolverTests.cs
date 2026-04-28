using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

public sealed class PeriodResolverTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 4, 28, 14, 30, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly PeriodResolver _resolver;

    public PeriodResolverTests()
    {
        _clock.Now.Returns(FixedNow);
        _resolver = new PeriodResolver(_clock);
    }

    [Fact]
    public void Resolve_AbsoluteRange_ReturnsBoundsAsIs()
    {
        DateTimeOffset from = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        PeriodSpec spec = new(from, to);

        ResolvedPeriod result = _resolver.Resolve(spec);

        result.From.ShouldBe(from);
        result.To.ShouldBe(to);
    }

    [Fact]
    public void Resolve_Today_ReturnsMidnightToTomorrow()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "today"));

        result.From.ShouldBe(new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero));
        result.To.ShouldBe(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_Yesterday_ReturnsPriorDayMidnightToToday()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "yesterday"));

        result.From.ShouldBe(new DateTimeOffset(2026, 4, 27, 0, 0, 0, TimeSpan.Zero));
        result.To.ShouldBe(new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_Last7Days_IncludesToday()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "last_7d"));

        result.From.ShouldBe(new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero));
        result.To.ShouldBe(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_Mtd_StartsAtFirstOfMonth()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "mtd"));

        result.From.ShouldBe(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        result.To.ShouldBe(new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_Qtd_StartsAtQuarterFirstMonth()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "qtd"));

        // April is in Q2, which starts in April.
        result.From.ShouldBe(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_Ytd_StartsAtJanuaryFirst()
    {
        ResolvedPeriod result = _resolver.Resolve(new PeriodSpec(Token: "ytd"));

        result.From.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_UnknownToken_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            _resolver.Resolve(new PeriodSpec(Token: "next_century")));
    }

    [Fact]
    public void Resolve_FromAfterTo_Throws()
    {
        DateTimeOffset from = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        Should.Throw<ArgumentException>(() => _resolver.Resolve(new PeriodSpec(from, to)));
    }

    [Fact]
    public void ResolveComparison_PreviousPeriod_ReturnsEqualLengthPrecedingWindow()
    {
        DateTimeOffset from = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        ResolvedPeriod main = new(from, to);

        ResolvedPeriod result = _resolver.ResolveComparison(new PeriodSpec(Token: "previous_period"), main);

        result.From.ShouldBe(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));
        result.To.ShouldBe(from);
    }
}
