using FluentValidation;
using Granit.Metering.Endpoints.Dtos;

namespace Granit.Metering.Endpoints.Validators;

internal sealed class MeterDefinitionCreateRequestValidator : AbstractValidator<MeterDefinitionCreateRequest>
{
    public MeterDefinitionCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Unit)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.AggregationType)
            .IsInEnum();

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}
