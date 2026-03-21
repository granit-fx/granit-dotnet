using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountDeleteRequest"/>.
/// </summary>
internal sealed class AccountDeleteRequestValidator : GranitValidator<AccountDeleteRequest>
{
    public AccountDeleteRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
