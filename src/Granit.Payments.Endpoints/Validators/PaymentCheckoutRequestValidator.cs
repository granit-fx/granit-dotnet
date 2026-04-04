using FluentValidation;
using Granit.Payments.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Payments.Endpoints.Validators;

internal sealed class PaymentCheckoutRequestValidator : AbstractValidator<PaymentCheckoutRequest>
{
    public PaymentCheckoutRequestValidator()
    {
        RuleFor(x => x.TransactionId)
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

        RuleFor(x => x.SuccessUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeHttpsUrl)
            .WithErrorCodeAndMessage("Granit:Validation:UrlMustBeHttps");

        RuleFor(x => x.CancelUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeHttpsUrl)
            .WithErrorCodeAndMessage("Granit:Validation:UrlMustBeHttps");

        RuleFor(x => x.ProviderName)
            .MaximumLength(50)
            .When(x => x.ProviderName is not null);
    }

    private static bool BeHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
}
