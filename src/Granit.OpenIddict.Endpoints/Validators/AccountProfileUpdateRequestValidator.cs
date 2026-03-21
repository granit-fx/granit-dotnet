using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AccountProfileUpdateRequest"/>.
/// </summary>
internal sealed class AccountProfileUpdateRequestValidator : GranitValidator<AccountProfileUpdateRequest>
{
    public AccountProfileUpdateRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(256);

        RuleFor(x => x.LastName)
            .MaximumLength(256);
    }
}
