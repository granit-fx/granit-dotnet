using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="PrivacyExportRequest"/>. Caps the requested-scopes
/// list size and per-entry length so the export request handler can't be used as
/// a DoS vector. Scope NAMES are deliberately NOT validated against a known set —
/// unknown / hidden entries are dropped server-side by the visibility resolver to
/// avoid enumerating the provider catalogue.
/// </summary>
internal sealed class PrivacyExportRequestValidator : GranitValidator<PrivacyExportRequest>
{
    private const int MaxScopes = 64;
    private const int MaxScopeNameLength = 200;

    public PrivacyExportRequestValidator()
    {
        RuleFor(x => x.Scopes)
            .Must(s => s is null || s.Count <= MaxScopes)
                .WithErrorCodeAndMessage("Privacy:Validation:TooManyScopes");

        RuleForEach(x => x.Scopes!)
            .NotEmpty()
            .MaximumLength(MaxScopeNameLength)
            .When(x => x.Scopes is { Count: > 0 });
    }
}
