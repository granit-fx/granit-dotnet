using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminOidcUpdateApplicationRequest"/>.
/// </summary>
internal sealed class AdminOidcUpdateApplicationRequestValidator : GranitValidator<AdminOidcUpdateApplicationRequest>
{
    public AdminOidcUpdateApplicationRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(256);
    }
}
