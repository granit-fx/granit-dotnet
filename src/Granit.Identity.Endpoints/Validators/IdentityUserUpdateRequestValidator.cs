using FluentValidation;
using Granit.Identity.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Identity.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="IdentityUserUpdateRequest"/> body for user updates.
/// </summary>
internal sealed class IdentityUserUpdateRequestValidator : GranitValidator<IdentityUserUpdateRequest>
{
    internal const int MaxEmailLength = 320;
    internal const int MaxNameLength = 256;
    internal const int MaxCustomAttributes = 50;

    public IdentityUserUpdateRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(MaxEmailLength)
            .When(x => x.Email is not null);

        RuleFor(x => x.FirstName)
            .MaximumLength(MaxNameLength)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(MaxNameLength)
            .When(x => x.LastName is not null);

        RuleFor(x => x.Attributes)
            .Must(a => a!.Count <= MaxCustomAttributes)
            .WithErrorCodeAndMessage("Granit:Validation:MaxCustomAttributes")
            .When(x => x.Attributes is not null);
    }
}
