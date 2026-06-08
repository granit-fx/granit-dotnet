using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminOidcCreateScopeRequest"/>.
/// </summary>
internal sealed class AdminOidcCreateScopeRequestValidator : GranitValidator<AdminOidcCreateScopeRequest>
{
    public AdminOidcCreateScopeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.DisplayName)
            .MaximumLength(256);

        RuleFor(x => x.Description)
            .MaximumLength(1024);

        RuleForEach(x => x.Resources)
            .NotEmpty()
            .MaximumLength(512)
            .When(x => x.Resources is not null);
    }
}
