using FluentValidation;
using Granit.Metering.Endpoints.Dtos;

namespace Granit.Metering.Endpoints.Validators;

internal sealed class RecordUsageRequestValidator : AbstractValidator<RecordUsageRequest>
{
    public RecordUsageRequestValidator()
    {
        RuleFor(x => x.Events)
            .NotEmpty();

        RuleForEach(x => x.Events)
            .SetValidator(new MeterEventRequestValidator());
    }
}

internal sealed class MeterEventRequestValidator : AbstractValidator<MeterEventRequest>
{
    public MeterEventRequestValidator()
    {
        RuleFor(x => x.MeterDefinitionId)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.Timestamp)
            .NotEmpty();
    }
}
