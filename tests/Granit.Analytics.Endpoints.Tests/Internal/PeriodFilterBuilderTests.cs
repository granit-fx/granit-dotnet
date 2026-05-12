using System.Linq.Expressions;
using Granit.Analytics.Internal;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

public sealed class PeriodFilterBuilderTests
{
    [Fact]
    public void ApplyPeriod_RestrictsToHalfOpenWindow()
    {
        // [from, to) — from is inclusive, to is exclusive.
        DateTimeOffset from = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        Sample[] data =
        [
            new("a", new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero)),
            new("b", from),
            new("c", new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero)),
            new("d", new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero)),
            new("e", to),
        ];

        Expression<Func<Sample, DateTimeOffset>> selector = s => s.At;

        IQueryable<Sample> filtered = PeriodFilterBuilder.ApplyPeriod(
            data.AsQueryable(),
            selector,
            new ResolvedPeriod(from, to));

        filtered.Select(s => s.Name).ToArray().ShouldBe(["b", "c", "d"]);
    }

    private sealed record Sample(string Name, DateTimeOffset At);
}
