using FluentValidation;
using Granit.Scheduling.Endpoints.Dtos;
using Granit.Timing;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Scheduling.Endpoints.Validators;

/// <summary>
/// Validates <see cref="RescheduleActionRequest"/>.
/// </summary>
internal sealed class RescheduleActionRequestValidator : GranitValidator<RescheduleActionRequest>
{
    public RescheduleActionRequestValidator(IClock clock)
    {
        RuleFor(x => x.NewExecuteAt)
            .Must(date => date > clock.Now)
            .WithErrorCodeAndMessage("Validation:ScheduledAction:FutureDate");
    }
}
