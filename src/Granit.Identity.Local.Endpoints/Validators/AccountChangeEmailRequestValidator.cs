using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountChangeEmailRequest"/>.
/// </summary>
internal sealed class AccountChangeEmailRequestValidator : GranitValidator<AccountChangeEmailRequest>
{
    public AccountChangeEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty();
    }
}
