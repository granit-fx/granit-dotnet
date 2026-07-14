using FluentValidation;
using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Http.Cookies.Endpoints.Validators;

/// <summary>Validates <see cref="ConsentDecisionRequest"/>.</summary>
/// <remarks>
/// Category names are checked against the snake_case vocabulary of
/// <see cref="CookieCategoryNames"/> — the same names <c>GET /cookies/config</c>
/// publishes to the CMP. Messages resolve from the module-owned
/// <c>Cookies:Validation:*</c> localization keys (ADR-066).
/// </remarks>
internal sealed class ConsentDecisionRequestValidator : GranitValidator<ConsentDecisionRequest>
{
    public ConsentDecisionRequestValidator()
    {
        RuleFor(x => x.CmpSource).NotEmpty().MaximumLength(64);

        RuleForEach(x => x.GrantedCategories)
            .Must(CookieCategoryNames.IsKnown)
            .WithErrorCodeAndMessage("Cookies:Validation:UnknownCategory");

        RuleForEach(x => x.DeniedCategories)
            .Must(CookieCategoryNames.IsKnown)
            .WithErrorCodeAndMessage("Cookies:Validation:UnknownCategory");

        RuleFor(x => x.GrantedCategories)
            .Must((request, granted) => granted is null
                || request.DeniedCategories is null
                || !granted.Intersect(request.DeniedCategories, StringComparer.Ordinal).Any())
            .WithErrorCodeAndMessage("Cookies:Validation:CategoryOverlap");

        // Anchored to GrantedCategories so the 422 payload carries a stable property key.
        RuleFor(x => x.GrantedCategories)
            .Must((request, _) => request.GrantedCategories is { Count: > 0 }
                || request.DeniedCategories is { Count: > 0 })
            .WithErrorCodeAndMessage("Cookies:Validation:EmptyDecision");
    }
}
