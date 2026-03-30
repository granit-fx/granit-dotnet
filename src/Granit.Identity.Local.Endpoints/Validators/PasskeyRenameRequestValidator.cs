using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

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
