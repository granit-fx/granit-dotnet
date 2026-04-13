using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="MigratePriceRequest"/>.
/// </summary>
internal sealed class MigratePriceRequestValidator : GranitValidator<MigratePriceRequest>
{
    public MigratePriceRequestValidator()
    {
        RuleFor(x => x.NewPlanPriceId)
            .NotEmpty();
    }
}
