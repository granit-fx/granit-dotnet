using Granit.AI.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.AI.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="AIWorkspaceUpdateRequest"/> body for AI workspace updates.
/// </summary>
internal sealed class AIWorkspaceUpdateRequestValidator : GranitValidator<AIWorkspaceUpdateRequest>
{
    public AIWorkspaceUpdateRequestValidator()
    {
        Include(new AIWorkspaceMutableFieldsValidator<AIWorkspaceUpdateRequest>());
    }
}
