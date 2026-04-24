using FluentValidation;
using Granit.Metering.Domain;
using Granit.Metering.Endpoints.Dtos;
using Granit.Validation.Extensions;

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

        RuleFor(x => x.DistinctProperty)
            .NotEmpty()
            .MaximumLength(200)
            .WithErrorCodeAndMessage("Granit:Validation:MeteringDistinctPropertyRequired")
            .When(x => x.AggregationType == AggregationType.CountDistinct);

        RuleFor(x => x.DistinctProperty)
            .Empty()
            .WithErrorCodeAndMessage("Granit:Validation:MeteringDistinctPropertyNotAllowed")
            .When(x => x.AggregationType != AggregationType.CountDistinct);
    }
}
