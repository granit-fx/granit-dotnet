using Granit.QueryEngine.SavedViews;
using Granit.Validation;

namespace Granit.QueryEngine.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="UpdateSavedViewRequest"/> body for saved view updates.
/// </summary>
internal sealed class UpdateSavedViewRequestValidator : GranitValidator<UpdateSavedViewRequest>
{
    public UpdateSavedViewRequestValidator()
    {
        Include(new SavedViewRequestValidator<UpdateSavedViewRequest>());
    }
}
