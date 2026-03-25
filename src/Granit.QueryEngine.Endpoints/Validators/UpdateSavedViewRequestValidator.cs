using FluentValidation;
using Granit.QueryEngine.SavedViews;
using Granit.Validation;

namespace Granit.QueryEngine.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="UpdateSavedViewRequest"/> body for saved view updates.
/// </summary>
internal sealed class UpdateSavedViewRequestValidator : GranitValidator<UpdateSavedViewRequest>
{
    internal const int MaxNameLength = 200;

    public UpdateSavedViewRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);
    }
}
