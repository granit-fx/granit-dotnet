using FluentValidation;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Authentication.ApiKeys.Endpoints.Validators;

/// <summary>
/// Validates <see cref="ApiKeyUpdateScopesRequest"/>.
/// </summary>
internal sealed class ApiKeyUpdateScopesRequestValidator : GranitValidator<ApiKeyUpdateScopesRequest>
{
    internal const int MaxPermissions = 100;
    internal const int MaxCidrs = 50;

    public ApiKeyUpdateScopesRequestValidator()
    {
        RuleFor(x => x.Permissions)
            .NotNull()
            .Must(p => p.Count <= MaxPermissions)
            .WithErrorCodeAndMessage("Validation:MaxPermissions");

        RuleForEach(x => x.Permissions)
            .NotEmpty();

        RuleFor(x => x.AllowedCidrs)
            .NotNull()
            .Must(c => c.Count <= MaxCidrs)
            .WithErrorCodeAndMessage("Validation:MaxCidrRanges");

        RuleForEach(x => x.AllowedCidrs)
            .NotEmpty()
            .Must(CidrValidator.IsValidCidr)
            .WithErrorCodeAndMessage("Validation:InvalidCidrNotation");
    }
}
