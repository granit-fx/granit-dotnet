using FluentValidation;
using Granit.Metering.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Metering.Endpoints.Validators;

internal sealed class RecomputeUsageRequestValidator : AbstractValidator<RecomputeUsageRequest>
{
    public RecomputeUsageRequestValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty();

        RuleFor(x => x.To)
            .NotEmpty()
            .GreaterThan(x => x.From)
            .WithErrorCodeAndMessage("Granit:Validation:MeteringRecomputeWindowInvalid");
    }
}
