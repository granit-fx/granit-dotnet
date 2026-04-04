using FluentValidation;
using Granit.Payments.Endpoints.Dtos;

namespace Granit.Payments.Endpoints.Validators;

internal sealed class PaymentAttachMethodRequestValidator : AbstractValidator<PaymentAttachMethodRequest>
{
    public PaymentAttachMethodRequestValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(500);
    }
}
