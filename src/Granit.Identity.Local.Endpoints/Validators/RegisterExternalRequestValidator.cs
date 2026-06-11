using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="RegisterExternalRequest"/>.
/// </summary>
internal sealed class RegisterExternalRequestValidator : GranitValidator<RegisterExternalRequest>
{
    public RegisterExternalRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.FirstName)
            .MaximumLength(256);

        RuleFor(x => x.LastName)
            .MaximumLength(256);
    }
}
