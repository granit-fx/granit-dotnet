using FluentValidation;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Local.Endpoints.Validators;

/// <summary>Validates <see cref="RoleUpdateRequest"/>.</summary>
internal sealed class RoleUpdateRequestValidator : GranitValidator<RoleUpdateRequest>
{
    public RoleUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(2048);
    }
}
