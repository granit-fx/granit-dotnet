using FluentValidation;
using Granit.QueryEngine.SavedViews;
using Granit.Validation;

namespace Granit.QueryEngine.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="CreateSavedViewRequest"/> body for saved view creation.
/// </summary>
internal sealed class CreateSavedViewRequestValidator : GranitValidator<CreateSavedViewRequest>
{
    internal const int MaxNameLength = 200;

    public CreateSavedViewRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);
    }
}
