using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountTwoFactorDisableRequest"/>.
/// </summary>
internal sealed class AccountTwoFactorDisableRequestValidator : GranitValidator<AccountTwoFactorDisableRequest>
{
    public AccountTwoFactorDisableRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
