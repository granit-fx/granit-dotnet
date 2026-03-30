using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountLoginRequest"/>.
/// </summary>
internal sealed class AccountLoginRequestValidator : GranitValidator<AccountLoginRequest>
{
    public AccountLoginRequestValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(256);
    }
}
