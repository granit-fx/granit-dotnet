using FluentValidation;
using Granit.AI.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Endpoints.Validators;

/// <summary>
/// Shared validation rules for <see cref="IAIWorkspaceMutableFields"/> fields.
/// Used by both <see cref="AIWorkspaceCreateRequestValidator"/> and
/// <see cref="AIWorkspaceUpdateRequestValidator"/> via <c>Include</c>.
/// </summary>
internal sealed class AIWorkspaceMutableFieldsValidator<T> : GranitValidator<T>
    where T : IAIWorkspaceMutableFields
{
    public AIWorkspaceMutableFieldsValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Model)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.SystemPrompt)
            .MaximumLength(32_000)
            .When(x => x.SystemPrompt is not null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0f, 2f)
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxOutputTokens)
            .GreaterThan(0)
            .When(x => x.MaxOutputTokens.HasValue);
    }
}
