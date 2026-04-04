using FluentValidation;
using Granit.CustomerBalance.Endpoints.Dtos;

namespace Granit.CustomerBalance.Endpoints.Validators;

internal sealed class AdminCreditRequestValidator : AbstractValidator<AdminCreditRequest>
{
    public AdminCreditRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
