using FluentValidation;
using Granit.Entities.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Entities.Endpoints.Validators;

internal sealed class CalendarRangeRequestValidator : AbstractValidator<CalendarRangeRequest>
{
    /// <summary>
    /// Maximum window the calendar endpoint is willing to fetch in one round-trip.
    /// One year is the largest band a typical calendar UI scrolls without hard
    /// re-fetching; anything wider risks dragging back a multi-million-row page
    /// that the renderer would just discard.
    /// </summary>
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(366);

    public CalendarRangeRequestValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithErrorCodeAndMessage("Granit:Validation:CalendarRangeInverted");

        RuleFor(x => x)
            .Must(x => x.To - x.From <= MaxRange)
            .WithErrorCodeAndMessage("Granit:Validation:CalendarRangeTooWide");

        RuleFor(x => x.Calendar)
            .MaximumLength(128)
            .When(x => x.Calendar is not null);
    }
}
