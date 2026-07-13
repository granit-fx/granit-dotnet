using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="PrivacyExportOnBehalfOfRequest"/>. Mirrors the
/// scope-list bounds from <see cref="PrivacyExportRequestValidator"/> and adds
/// the dedicated <c>SubjectUserId</c> non-empty check — the admin DSR path
/// requires a real subject identifier, so an empty Guid is rejected at the
/// FluentValidation pass before the handler runs.
/// </summary>
internal sealed class PrivacyExportOnBehalfOfRequestValidator : GranitValidator<PrivacyExportOnBehalfOfRequest>
{
    private const int MaxScopes = 64;
    private const int MaxScopeNameLength = 200;

    public PrivacyExportOnBehalfOfRequestValidator()
    {
        RuleFor(x => x.SubjectUserId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Scopes)
            .Must(s => s is null || s.Count <= MaxScopes)
                .WithErrorCodeAndMessage("Privacy:Validation:TooManyScopes");

        RuleForEach(x => x.Scopes!)
            .NotEmpty()
            .MaximumLength(MaxScopeNameLength)
            .When(x => x.Scopes is { Count: > 0 });
    }
}
