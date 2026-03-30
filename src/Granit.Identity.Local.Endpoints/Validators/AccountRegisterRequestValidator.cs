using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountRegisterRequest"/>.
/// </summary>
internal sealed class AccountRegisterRequestValidator : GranitValidator<AccountRegisterRequest>
{
    public AccountRegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(x => x.FirstName)
            .MaximumLength(256);

        RuleFor(x => x.LastName)
            .MaximumLength(256);
    }
}
