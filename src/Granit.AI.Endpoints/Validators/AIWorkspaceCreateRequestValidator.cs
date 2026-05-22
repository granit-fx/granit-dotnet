using FluentValidation;
using Granit.AI.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.AI.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="AIWorkspaceCreateRequest"/> body for AI workspace creation.
/// </summary>
internal sealed class AIWorkspaceCreateRequestValidator : GranitValidator<AIWorkspaceCreateRequest>
{
    public AIWorkspaceCreateRequestValidator()
    {
        Include(new AIWorkspaceMutableFieldsValidator<AIWorkspaceCreateRequest>());

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithErrorCodeAndMessage("Validation:InvalidWorkspaceName");
    }
}
