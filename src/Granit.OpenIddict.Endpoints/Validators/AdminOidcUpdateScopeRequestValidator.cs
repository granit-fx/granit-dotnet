using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminOidcUpdateScopeRequest"/>.
/// </summary>
internal sealed class AdminOidcUpdateScopeRequestValidator : GranitValidator<AdminOidcUpdateScopeRequest>
{
    public AdminOidcUpdateScopeRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}
