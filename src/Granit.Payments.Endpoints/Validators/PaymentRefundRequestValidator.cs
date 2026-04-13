using FluentValidation;
using Granit.Payments.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.Payments.Endpoints.Validators;

internal sealed class PaymentRefundRequestValidator : AbstractValidator<PaymentRefundRequest>
{
    public PaymentRefundRequestValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999_999_999.99m)
            .WithErrorCodeAndMessage("Granit:Validation:PaymentAmountOutOfRange");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null);
    }
}
