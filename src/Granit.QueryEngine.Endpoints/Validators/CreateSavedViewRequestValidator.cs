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
    internal const int MaxJsonLength = 10_000;

    public CreateSavedViewRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.FilterJson)
            .MaximumLength(MaxJsonLength)
            .When(x => x.FilterJson is not null);

        RuleFor(x => x.SortJson)
            .MaximumLength(MaxJsonLength)
            .When(x => x.SortJson is not null);

        RuleFor(x => x.GroupByJson)
            .MaximumLength(MaxJsonLength)
            .When(x => x.GroupByJson is not null);

        RuleFor(x => x.VisibleColumnsJson)
            .MaximumLength(MaxJsonLength)
            .When(x => x.VisibleColumnsJson is not null);
    }
}
