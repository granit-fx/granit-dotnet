using Granit.QueryEngine.SavedViews;
using Granit.Validation;

namespace Granit.QueryEngine.AspNetCore.Validators;

/// <summary>
/// Validates the <see cref="CreateSavedViewRequest"/> body for saved view creation.
/// </summary>
internal sealed class CreateSavedViewRequestValidator : GranitValidator<CreateSavedViewRequest>
{
    public CreateSavedViewRequestValidator()
    {
        Include(new SavedViewRequestValidator<CreateSavedViewRequest>());
    }
}
