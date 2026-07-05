using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Layouts;

public sealed class CalendarRangeFilterBuilderTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartOnly_layout_keeps_only_events_inside_the_window()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = nameof(PointInTimeEvent.OccurredAt),
        };

        Func<PointInTimeEvent, bool> filter = CalendarRangeFilterBuilder
            .BuildOverlapFilter<PointInTimeEvent>(layout, new CalendarFilterRange(Anchor, Anchor.AddDays(7)))
            .Compile();

        filter(new PointInTimeEvent { OccurredAt = Anchor.AddDays(-1) }).ShouldBeFalse();
        filter(new PointInTimeEvent { OccurredAt = Anchor }).ShouldBeTrue();
        filter(new PointInTimeEvent { OccurredAt = Anchor.AddDays(3) }).ShouldBeTrue();
        filter(new PointInTimeEvent { OccurredAt = Anchor.AddDays(7) }).ShouldBeTrue();
        filter(new PointInTimeEvent { OccurredAt = Anchor.AddDays(8) }).ShouldBeFalse();
    }

    [Fact]
    public void StartEnd_layout_keeps_events_overlapping_the_window()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = nameof(Meeting.StartsAt),
            EndPropertyName = nameof(Meeting.EndsAt),
        };

        Func<Meeting, bool> filter = CalendarRangeFilterBuilder
            .BuildOverlapFilter<Meeting>(layout, new CalendarFilterRange(Anchor, Anchor.AddDays(7)))
            .Compile();

        // Entirely before the window
        filter(new Meeting { StartsAt = Anchor.AddDays(-3), EndsAt = Anchor.AddDays(-2) }).ShouldBeFalse();
        // Spans the window
        filter(new Meeting { StartsAt = Anchor.AddDays(-3), EndsAt = Anchor.AddDays(10) }).ShouldBeTrue();
        // Starts before, ends inside
        filter(new Meeting { StartsAt = Anchor.AddDays(-1), EndsAt = Anchor.AddDays(2) }).ShouldBeTrue();
        // Starts inside, ends after
        filter(new Meeting { StartsAt = Anchor.AddDays(5), EndsAt = Anchor.AddDays(10) }).ShouldBeTrue();
        // Entirely inside
        filter(new Meeting { StartsAt = Anchor.AddDays(1), EndsAt = Anchor.AddDays(2) }).ShouldBeTrue();
        // Entirely after
        filter(new Meeting { StartsAt = Anchor.AddDays(8), EndsAt = Anchor.AddDays(10) }).ShouldBeFalse();
        // Touches the boundary at the start
        filter(new Meeting { StartsAt = Anchor.AddDays(-1), EndsAt = Anchor }).ShouldBeTrue();
        // Touches the boundary at the end
        filter(new Meeting { StartsAt = Anchor.AddDays(7), EndsAt = Anchor.AddDays(10) }).ShouldBeTrue();
    }

    [Fact]
    public void Open_ended_event_is_kept_when_start_is_inside_the_window()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = nameof(Meeting.StartsAt),
            EndPropertyName = nameof(Meeting.EndsAt),
        };

        Func<Meeting, bool> filter = CalendarRangeFilterBuilder
            .BuildOverlapFilter<Meeting>(layout, new CalendarFilterRange(Anchor, Anchor.AddDays(7)))
            .Compile();

        // Open-ended (EndsAt null) inside window — kept because End coalesces to Start, satisfying from <= Start.
        filter(new Meeting { StartsAt = Anchor.AddDays(2), EndsAt = null }).ShouldBeTrue();
        // Open-ended starting before window — dropped (Start <= to is true but from <= (End ?? Start) = from <= Start fails).
        filter(new Meeting { StartsAt = Anchor.AddDays(-1), EndsAt = null }).ShouldBeFalse();
        // Open-ended starting after window — Start <= to fails, so dropped.
        filter(new Meeting { StartsAt = Anchor.AddDays(10), EndsAt = null }).ShouldBeFalse();
    }

    [Fact]
    public void BuildOverlapFilter_throws_when_StartField_does_not_exist_on_TEntity()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = "DoesNotExist",
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CalendarRangeFilterBuilder.BuildOverlapFilter<Meeting>(layout, new CalendarFilterRange(Anchor, Anchor)));

        ex.Message.ShouldContain("DoesNotExist");
        ex.Message.ShouldContain("StartField");
    }

    [Fact]
    public void BuildOverlapFilter_throws_when_EndField_does_not_exist_on_TEntity()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = nameof(Meeting.StartsAt),
            EndPropertyName = "DoesNotExist",
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CalendarRangeFilterBuilder.BuildOverlapFilter<Meeting>(layout, new CalendarFilterRange(Anchor, Anchor)));

        ex.Message.ShouldContain("DoesNotExist");
        ex.Message.ShouldContain("EndField");
    }

    [Fact]
    public void Compiled_filter_supports_LINQ_to_objects_pipeline()
    {
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = nameof(Meeting.StartsAt),
            EndPropertyName = nameof(Meeting.EndsAt),
        };

        IReadOnlyList<Meeting> meetings =
        [
            new Meeting { Id = Guid.NewGuid(), StartsAt = Anchor.AddDays(-5), EndsAt = Anchor.AddDays(-4) }, // before
            new Meeting { Id = Guid.NewGuid(), StartsAt = Anchor.AddDays(1), EndsAt = Anchor.AddDays(2) },   // inside
            new Meeting { Id = Guid.NewGuid(), StartsAt = Anchor.AddDays(10), EndsAt = Anchor.AddDays(11) }, // after
        ];

        Func<Meeting, bool> filter = CalendarRangeFilterBuilder
            .BuildOverlapFilter<Meeting>(layout, new CalendarFilterRange(Anchor, Anchor.AddDays(7)))
            .Compile();

        meetings.Count(filter).ShouldBe(1);
    }

    private sealed class PointInTimeEvent
    {
        public DateTimeOffset OccurredAt { get; set; }
    }

    private sealed class Meeting
    {
        public Guid Id { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
    }
}
