using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.OpenIddict.Endpoints.Validators;

/// <summary>
/// Validates <see cref="AdminOidcCreateAuthorizationRequest"/>.
/// </summary>
internal sealed class AdminOidcCreateAuthorizationRequestValidator : GranitValidator<AdminOidcCreateAuthorizationRequest>
{
    public AdminOidcCreateAuthorizationRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Scopes)
            .NotEmpty();
    }
}
