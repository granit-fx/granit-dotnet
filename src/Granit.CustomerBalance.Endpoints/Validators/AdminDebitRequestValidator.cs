using FluentValidation;
using Granit.CustomerBalance.Endpoints.Dtos;

namespace Granit.CustomerBalance.Endpoints.Validators;

internal sealed class AdminDebitRequestValidator : AbstractValidator<AdminDebitRequest>
{
    public AdminDebitRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ReferenceType).MaximumLength(64).When(x => x.ReferenceType is not null);
    }
}
