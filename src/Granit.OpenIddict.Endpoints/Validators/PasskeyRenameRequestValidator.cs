using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PasskeyRenameRequest"/>.
/// </summary>
internal sealed class PasskeyRenameRequestValidator : GranitValidator<PasskeyRenameRequest>
{
    public PasskeyRenameRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
