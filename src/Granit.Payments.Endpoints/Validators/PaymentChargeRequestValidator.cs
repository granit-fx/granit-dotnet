using FluentValidation;
using Granit.Payments.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Payments.Endpoints.Validators;

internal sealed class PaymentChargeRequestValidator : AbstractValidator<PaymentChargeRequest>
{
    public PaymentChargeRequestValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999.99m)
            .WithErrorCodeAndMessage("Granit:Validation:PaymentAmountOutOfRange");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches(@"^[A-Z]{3}$")
            .WithErrorCodeAndMessage("Granit:Validation:InvalidIso4217CurrencyCode");

        RuleFor(x => x.MethodType)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.ProviderName)
            .MaximumLength(50)
            .When(x => x.ProviderName is not null);
    }
}
