using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="CreatePriceVersionRequest"/>.
/// </summary>
internal sealed class CreatePriceVersionRequestValidator : GranitValidator<CreatePriceVersionRequest>
{
    internal const int MaxCurrencyLength = 3;

    public CreatePriceVersionRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .MaximumLength(MaxCurrencyLength);

        RuleFor(x => x.Interval)
            .NotEmpty();
    }
}
