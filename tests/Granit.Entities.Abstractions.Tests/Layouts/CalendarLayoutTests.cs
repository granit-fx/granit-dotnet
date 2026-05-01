using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Layouts;

public sealed class CalendarLayoutTests
{
    [Fact]
    public void Descriptor_carries_start_end_title_and_color_by()
    {
        EntityDefinitionDescriptor d = new SampleMeetingDefinition().Descriptor;

        d.ListLayouts.ShouldHaveSingleItem();
        CalendarLayoutDescriptor calendar = d.ListLayouts.Single().ShouldBeOfType<CalendarLayoutDescriptor>();

        calendar.Kind.ShouldBe(EntityListLayoutKind.Calendar);
        calendar.StartPropertyName.ShouldBe("StartsAt");
        calendar.EndPropertyName.ShouldBe("EndsAt");
        calendar.TitlePropertyName.ShouldBe("Subject");
        calendar.ColorByPropertyName.ShouldBe("Status");
    }

    [Fact]
    public void IsDefault_and_RequiresPermission_round_trip()
    {
        EntityDefinitionDescriptor d = new SampleMeetingDefinition().Descriptor;
        EntityListLayoutDescriptor calendar = d.ListLayouts.Single();

        calendar.IsDefault.ShouldBeTrue();
        calendar.RequiresPermission.ShouldBe("Meetings.Meetings.Calendar");
    }

    [Fact]
    public void End_title_and_color_by_are_optional()
    {
        EntityDefinitionDescriptor d = new MinimalCalendarDefinition().Descriptor;
        var calendar = (CalendarLayoutDescriptor)d.ListLayouts.Single();

        calendar.StartPropertyName.ShouldBe("StartsAt");
        calendar.EndPropertyName.ShouldBeNull();
        calendar.TitlePropertyName.ShouldBeNull();
        calendar.ColorByPropertyName.ShouldBeNull();
    }

    [Fact]
    public void Build_rejects_missing_start_field()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new MissingStartFieldDefinition().Descriptor);

        ex.Message.ShouldContain("StartField");
    }

    [Fact]
    public void Build_rejects_non_property_start_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadStartLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("StartField selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_non_property_title_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadTitleLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("TitleField selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_non_property_color_by_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadColorByLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("ColorBy selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_two_default_layouts()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new TwoDefaultsDefinition().Descriptor);

        ex.Message.ShouldContain("At most one list-view layout may be marked IsDefault");
    }

    [Fact]
    public void Build_rejects_duplicate_calendar_kinds()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new DuplicateCalendarsDefinition().Descriptor);

        ex.Message.ShouldContain("Duplicate list-view layout kind 'Calendar'");
    }

    private enum SampleMeetingStatus { Tentative, Confirmed, Cancelled }

    private sealed class SampleMeeting
    {
        public string Subject { get; set; } = string.Empty;
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
        public SampleMeetingStatus Status { get; set; }
    }

    private sealed class SampleMeetingDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.Meeting";

        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c
                .IsDefault()
                .RequiresPermission("Meetings.Meetings.Calendar")
                .StartField(m => m.StartsAt)
                .EndField(m => m.EndsAt)
                .TitleField(m => m.Subject)
                .ColorBy(m => m.Status));
    }

    private sealed class MinimalCalendarDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.MinimalCalendar";

        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c.StartField(m => m.StartsAt));
    }

    private sealed class MissingStartFieldDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.MissingStart";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c.TitleField(m => m.Subject));
    }

    private sealed class BadStartLambdaDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.BadStart";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c.StartField(m => m.StartsAt.AddHours(1)));
    }

    private sealed class BadTitleLambdaDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.BadTitle";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c
                .StartField(m => m.StartsAt)
                .TitleField(m => m.Subject.ToUpperInvariant()));
    }

    private sealed class BadColorByLambdaDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.BadColorBy";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder.CalendarView(c => c
                .StartField(m => m.StartsAt)
                .ColorBy(m => m.Status.ToString()));
    }

    private sealed class TwoDefaultsDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.TwoCalendarDefaults";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder
                .CalendarView(c => c.IsDefault().StartField(m => m.StartsAt))
                .KanbanView<SampleMeetingStatus>(k => k.IsDefault().GroupBy(m => m.Status));
    }

    private sealed class DuplicateCalendarsDefinition : EntityDefinition<SampleMeeting>
    {
        public override string Name => "Granit.Sample.DuplicateCalendars";
        protected override void Configure(EntityDefinitionBuilder<SampleMeeting> builder) =>
            builder
                .CalendarView(c => c.StartField(m => m.StartsAt))
                .CalendarView(c => c.StartField(m => m.StartsAt));
    }
}
