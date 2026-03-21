using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

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
