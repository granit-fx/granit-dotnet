using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountPasswordChangeRequest"/>.
/// </summary>
internal sealed class AccountPasswordChangeRequestValidator : GranitValidator<AccountPasswordChangeRequest>
{
    public AccountPasswordChangeRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}
