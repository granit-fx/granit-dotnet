using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminOidcCreateApplicationRequest"/>.
/// </summary>
internal sealed class AdminOidcCreateApplicationRequestValidator : GranitValidator<AdminOidcCreateApplicationRequest>
{
    public AdminOidcCreateApplicationRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.DisplayName)
            .MaximumLength(256);
    }
}
