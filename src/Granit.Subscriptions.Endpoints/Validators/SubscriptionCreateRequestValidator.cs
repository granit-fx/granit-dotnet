using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SubscriptionCreateRequest"/>.
/// </summary>
internal sealed class SubscriptionCreateRequestValidator : GranitValidator<SubscriptionCreateRequest>
{
    internal const int MaxCurrencyLength = 3;

    public SubscriptionCreateRequestValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty();

        RuleFor(x => x.Currency)
            .NotEmpty()
            .MaximumLength(MaxCurrencyLength);
    }
}
