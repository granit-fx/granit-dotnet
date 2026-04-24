using FluentValidation;
using Granit.Metering.Domain;
using Granit.Metering.Endpoints.Dtos;

namespace Granit.Metering.Endpoints.Validators;

internal sealed class DeprecateEventRequestValidator : AbstractValidator<DeprecateEventRequest>
{
    public DeprecateEventRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(MeterEvent.DeprecationReasonMaxLength);
    }
}
