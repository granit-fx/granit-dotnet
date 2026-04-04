using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountConfirmEmailChangeRequest"/>.
/// </summary>
internal sealed class AccountConfirmEmailChangeRequestValidator : GranitValidator<AccountConfirmEmailChangeRequest>
{
    public AccountConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.NewEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
