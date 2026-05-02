using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class CalendarRangeETagTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compute_returns_quoted_strong_etag()
    {
        IReadOnlyList<CalendarItemResponse> items =
        [
            new CalendarItemResponse(Guid.NewGuid(), Anchor, null, "X", null),
        ];

        string etag = CalendarRangeETag.Compute(items);

        etag.ShouldStartWith("\"");
        etag.ShouldEndWith("\"");
        etag.Length.ShouldBe(34); // 32 hex + 2 quotes
    }

    [Fact]
    public void Compute_same_items_same_etag()
    {
        var id = Guid.NewGuid();
        IReadOnlyList<CalendarItemResponse> a = [new(id, Anchor, null, "X", null)];
        IReadOnlyList<CalendarItemResponse> b = [new(id, Anchor, null, "X", null)];

        CalendarRangeETag.Compute(a).ShouldBe(CalendarRangeETag.Compute(b));
    }

    [Fact]
    public void Compute_different_items_different_etag()
    {
        IReadOnlyList<CalendarItemResponse> a = [new(Guid.NewGuid(), Anchor, null, "X", null)];
        IReadOnlyList<CalendarItemResponse> b = [new(Guid.NewGuid(), Anchor, null, "X", null)];

        CalendarRangeETag.Compute(a).ShouldNotBe(CalendarRangeETag.Compute(b));
    }
}
