using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PasskeyRegistrationRequest"/>.
/// </summary>
internal sealed class PasskeyRegistrationRequestValidator : GranitValidator<PasskeyRegistrationRequest>
{
    public PasskeyRegistrationRequestValidator()
    {
        RuleFor(x => x.CredentialJson)
            .NotEmpty();

        RuleFor(x => x.Name)
            .MaximumLength(100);
    }
}
