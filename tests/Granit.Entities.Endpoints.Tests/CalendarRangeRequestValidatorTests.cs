using FluentValidation.Results;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class CalendarRangeRequestValidatorTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly CalendarRangeRequestValidator _sut = new();

    [Fact]
    public void Same_From_and_To_is_valid()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor, null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void One_month_window_is_valid()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor.AddDays(30), null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void To_before_From_is_rejected_with_inverted_code()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor.AddDays(-1), null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Granit:Validation:CalendarRangeInverted");
    }

    [Fact]
    public void Window_wider_than_366_days_is_rejected_with_too_wide_code()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor.AddDays(367), null));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "Granit:Validation:CalendarRangeTooWide");
    }

    [Fact]
    public void Window_at_max_range_366_days_is_valid()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor.AddDays(366), null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Calendar_name_longer_than_128_chars_is_rejected()
    {
        string oversize = new('a', 129);
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor, oversize));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Null_calendar_name_is_accepted()
    {
        ValidationResult result = _sut.Validate(new CalendarRangeRequest(Anchor, Anchor, null));
        result.IsValid.ShouldBeTrue();
    }
}
