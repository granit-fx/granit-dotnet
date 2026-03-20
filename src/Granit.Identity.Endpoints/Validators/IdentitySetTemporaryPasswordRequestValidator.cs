using FluentValidation;
using Granit.Identity.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Identity.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="IdentitySetTemporaryPasswordRequest"/> body.
/// </summary>
internal sealed class IdentitySetTemporaryPasswordRequestValidator : GranitValidator<IdentitySetTemporaryPasswordRequest>
{
    internal const int MinPasswordLength = 8;
    internal const int MaxPasswordLength = 128;

    public IdentitySetTemporaryPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength);
    }
}
