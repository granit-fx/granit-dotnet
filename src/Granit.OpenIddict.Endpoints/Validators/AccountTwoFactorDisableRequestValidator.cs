using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

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
