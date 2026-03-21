using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

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
