using FluentValidation;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

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

        RuleForEach(x => x.RedirectUris)
            .NotEmpty()
            .AbsoluteUri()
            .When(x => x.RedirectUris is not null);

        RuleForEach(x => x.PostLogoutRedirectUris)
            .NotEmpty()
            .AbsoluteUri()
            .When(x => x.PostLogoutRedirectUris is not null);
    }
}
