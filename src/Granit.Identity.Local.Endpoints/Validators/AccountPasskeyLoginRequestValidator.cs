using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountPasskeyLoginRequest"/>.
/// </summary>
internal sealed class AccountPasskeyLoginRequestValidator : GranitValidator<AccountPasskeyLoginRequest>
{
    public AccountPasskeyLoginRequestValidator()
    {
        RuleFor(x => x.CredentialJson)
            .NotEmpty();
    }
}
