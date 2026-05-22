using FluentValidation;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Timing;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Authentication.ApiKeys.Endpoints.Validators;

/// <summary>
/// Validates <see cref="ApiKeyCreateRequest"/>.
/// </summary>
internal sealed class ApiKeyCreateRequestValidator : GranitValidator<ApiKeyCreateRequest>
{
    internal const int MaxNameLength = 200;
    internal const int MaxPermissions = 100;
    internal const int MaxCidrs = 50;

    public ApiKeyCreateRequestValidator(IClock clock)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Environment)
            .NotEmpty()
            .Must(env => env is "live" or "test" or "dev")
            .WithErrorCodeAndMessage("Validation:InvalidEnvironment");

        RuleFor(x => x.Permissions)
            .Must(p => p is null || p.Count <= MaxPermissions)
            .WithErrorCodeAndMessage("Validation:MaxPermissions");

        RuleForEach(x => x.Permissions)
            .NotEmpty()
            .When(x => x.Permissions is { Count: > 0 });

        RuleFor(x => x.AllowedCidrs)
            .Must(c => c is null || c.Count <= MaxCidrs)
            .WithErrorCodeAndMessage("Validation:MaxCidrRanges");

        RuleForEach(x => x.AllowedCidrs)
            .NotEmpty()
            .Must(CidrValidator.IsValidCidr)
            .WithErrorCodeAndMessage("Validation:InvalidCidrNotation")
            .When(x => x.AllowedCidrs is { Count: > 0 });

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt > clock.Now)
            .When(x => x.ExpiresAt.HasValue)
            .WithErrorCodeAndMessage("Validation:ExpirationMustBeFuture");

        RuleFor(x => x.CacheBehavior)
            .IsInEnum();
    }
}
