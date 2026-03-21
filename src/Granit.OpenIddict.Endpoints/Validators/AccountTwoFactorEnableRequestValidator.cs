using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountTwoFactorEnableRequest"/>.
/// </summary>
internal sealed class AccountTwoFactorEnableRequestValidator : GranitValidator<AccountTwoFactorEnableRequest>
{
    public AccountTwoFactorEnableRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(6);
    }
}
