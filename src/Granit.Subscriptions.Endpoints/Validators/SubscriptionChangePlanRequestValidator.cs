using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SubscriptionChangePlanRequest"/>.
/// </summary>
internal sealed class SubscriptionChangePlanRequestValidator : GranitValidator<SubscriptionChangePlanRequest>
{
    public SubscriptionChangePlanRequestValidator()
    {
        RuleFor(x => x.NewPlanId)
            .NotEmpty();
    }
}
