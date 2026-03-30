using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountForgotPasswordRequest"/>.
/// </summary>
internal sealed class AccountForgotPasswordRequestValidator : GranitValidator<AccountForgotPasswordRequest>
{
    public AccountForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
