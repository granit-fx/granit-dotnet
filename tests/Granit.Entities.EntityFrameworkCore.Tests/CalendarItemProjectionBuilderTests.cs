using Granit.Domain;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.EntityFrameworkCore.Tests;

public sealed class CalendarItemProjectionBuilderTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    public sealed class SampleEvent : Entity
    {
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public EventBucket Bucket { get; set; }
        public EventBucket? OptionalBucket { get; set; }
    }

    public enum EventBucket { Internal, External, Cancelled }

    [Fact]
    public void Build_AllFields_ProjectsCorrectly()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            EndPropertyName = nameof(SampleEvent.EndsAt),
            TitlePropertyName = nameof(SampleEvent.Subject),
            ColorByPropertyName = nameof(SampleEvent.Bucket),
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        var id = Guid.NewGuid();
        var entity = new SampleEvent()
        {
            Id = id,
            StartsAt = Anchor,
            EndsAt = Anchor.AddHours(1),
            Subject = "Standup",
            Bucket = EventBucket.Internal,
        };

        CalendarItemResponse result = projection(entity);

        result.Id.ShouldBe(id);
        result.Start.ShouldBe(Anchor);
        result.End.ShouldBe(Anchor.AddHours(1));
        result.Title.ShouldBe("Standup");
        result.Color.ShouldBe("Internal");
    }

    [Fact]
    public void Build_OmittedEnd_ReturnsNullEnd()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            TitlePropertyName = nameof(SampleEvent.Subject),
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        CalendarItemResponse result = projection(new SampleEvent
        {
            Id = Guid.NewGuid(),
            StartsAt = Anchor,
            Subject = "Birthday",
            EndsAt = Anchor.AddYears(1), // ignored — layout omits End
        });

        result.End.ShouldBeNull();
    }

    [Fact]
    public void Build_TitleFallsBackToDisplayProperty()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, nameof(SampleEvent.DisplayName)).Compile();

        CalendarItemResponse result = projection(new SampleEvent
        {
            Id = Guid.NewGuid(),
            StartsAt = Anchor,
            DisplayName = "Conference",
        });

        result.Title.ShouldBe("Conference");
    }

    [Fact]
    public void Build_TitleFallsBackToIdString_WhenNoDisplayProperty()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        var id = Guid.NewGuid();
        CalendarItemResponse result = projection(new SampleEvent { Id = id, StartsAt = Anchor });

        result.Title.ShouldBe(id.ToString());
    }

    [Fact]
    public void Build_NoColorBy_ReturnsNullColor()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            TitlePropertyName = nameof(SampleEvent.Subject),
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        CalendarItemResponse result = projection(new SampleEvent
        {
            Id = Guid.NewGuid(),
            StartsAt = Anchor,
            Subject = "Standup",
        });

        result.Color.ShouldBeNull();
    }

    [Fact]
    public void Build_NullableColor_ProjectsNullForNullValue()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            ColorByPropertyName = nameof(SampleEvent.OptionalBucket),
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        CalendarItemResponse result = projection(new SampleEvent
        {
            Id = Guid.NewGuid(),
            StartsAt = Anchor,
            OptionalBucket = null,
        });

        result.Color.ShouldBeNull();
    }

    [Fact]
    public void Build_NullableColor_ProjectsToStringForValue()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            ColorByPropertyName = nameof(SampleEvent.OptionalBucket),
            IsDefault = true,
        };

        Func<SampleEvent, CalendarItemResponse> projection =
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null).Compile();

        CalendarItemResponse result = projection(new SampleEvent
        {
            Id = Guid.NewGuid(),
            StartsAt = Anchor,
            OptionalBucket = EventBucket.External,
        });

        result.Color.ShouldBe("External");
    }

    [Fact]
    public void Build_MissingStartProperty_ThrowsAtBuildTime()
    {
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = "DoesNotExist",
            Kind = EntityListLayoutKind.Calendar,
            IsDefault = true,
        };

        Should.Throw<InvalidOperationException>(() =>
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null));
    }

    [Fact]
    public void Build_StartPropertyWrongType_ThrowsAtBuildTime()
    {
        // Subject is string, not DateTimeOffset
        var layout = new CalendarLayoutDescriptor()
        {
            StartPropertyName = nameof(SampleEvent.Subject),
            Kind = EntityListLayoutKind.Calendar,
            IsDefault = true,
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            CalendarItemProjectionBuilder.Build<SampleEvent>(layout, displayProperty: null));

        ex.Message.ShouldContain("DateTimeOffset");
    }
}
