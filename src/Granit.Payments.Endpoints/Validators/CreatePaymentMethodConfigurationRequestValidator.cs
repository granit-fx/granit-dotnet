using FluentValidation;
using Granit.Payments.Endpoints.Dtos;

namespace Granit.Payments.Endpoints.Validators;

internal sealed class CreatePaymentMethodConfigurationRequestValidator
    : AbstractValidator<CreatePaymentMethodConfigurationRequest>
{
    public CreatePaymentMethodConfigurationRequestValidator()
    {
        RuleFor(x => x.MethodType)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.DisplayLabel)
            .NotEmpty()
            .MaximumLength(100);
    }
}
