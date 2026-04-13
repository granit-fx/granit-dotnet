using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="BulkMigratePriceRequest"/>.
/// </summary>
internal sealed class BulkMigratePriceRequestValidator : GranitValidator<BulkMigratePriceRequest>
{
    public BulkMigratePriceRequestValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.NewPlanPriceId)
            .NotEmpty();
    }
}
