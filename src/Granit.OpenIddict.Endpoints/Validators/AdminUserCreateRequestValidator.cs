using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminUserCreateRequest"/>.
/// </summary>
internal sealed class AdminUserCreateRequestValidator : GranitValidator<AdminUserCreateRequest>
{
    public AdminUserCreateRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.FirstName)
            .MaximumLength(256);

        RuleFor(x => x.LastName)
            .MaximumLength(256);

        RuleFor(x => x.TemporaryPassword)
            .MinimumLength(8)
            .When(x => x.TemporaryPassword is not null);
    }
}
