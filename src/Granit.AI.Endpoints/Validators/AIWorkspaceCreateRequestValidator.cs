using FluentValidation;
using Granit.AI.Endpoints.Dtos;
using Granit.Validation.Extensions;

namespace Granit.AI.Endpoints.Validators;

internal sealed class AIWorkspaceCreateRequestValidator : AbstractValidator<AIWorkspaceCreateRequest>
{
    public AIWorkspaceCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithErrorCodeAndMessage("Granit:Validation:InvalidWorkspaceName");

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
