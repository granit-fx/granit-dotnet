using FluentValidation;
using Granit.Identity.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="IdentityUserCreateRequest"/> body for user creation.
/// </summary>
internal sealed class IdentityUserCreateRequestValidator : GranitValidator<IdentityUserCreateRequest>
{
    internal const int MaxUsernameLength = 256;
    internal const int MaxEmailLength = 320;
    internal const int MaxNameLength = 256;
    internal const int MinPasswordLength = 8;
    internal const int MaxPasswordLength = 128;

    public IdentityUserCreateRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(MaxUsernameLength);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(MaxEmailLength);

        RuleFor(x => x.FirstName)
            .MaximumLength(MaxNameLength)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(MaxNameLength)
            .When(x => x.LastName is not null);

        RuleFor(x => x.TemporaryPassword)
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength)
            .When(x => x.TemporaryPassword is not null);
    }
}
