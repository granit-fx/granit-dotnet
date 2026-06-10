using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountTwoFactorEmailEnableRequest"/>.
/// </summary>
internal sealed class AccountTwoFactorEmailEnableRequestValidator : GranitValidator<AccountTwoFactorEmailEnableRequest>
{
    public AccountTwoFactorEmailEnableRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(6);
    }
}
