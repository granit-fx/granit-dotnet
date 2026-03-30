using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

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
