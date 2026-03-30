using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountTwoFactorLoginRequest"/>.
/// </summary>
internal sealed class AccountTwoFactorLoginRequestValidator : GranitValidator<AccountTwoFactorLoginRequest>
{
    public AccountTwoFactorLoginRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(256);
    }
}
