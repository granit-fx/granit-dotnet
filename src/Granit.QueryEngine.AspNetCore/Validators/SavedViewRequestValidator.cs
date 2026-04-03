using FluentValidation;
using Granit.QueryEngine.SavedViews;
using Granit.Validation;

namespace Granit.QueryEngine.AspNetCore.Validators;

/// <summary>
/// Shared validation rules for <see cref="ISavedViewRequest"/> fields.
/// Used by both <see cref="CreateSavedViewRequestValidator"/> and
/// <see cref="UpdateSavedViewRequestValidator"/> via <c>Include</c>.
/// </summary>
internal sealed class SavedViewRequestValidator<T> : GranitValidator<T>
    where T : ISavedViewRequest
{
    internal const int MaxNameLength = 200;
    internal const int MaxJsonLength = 10_000;

    public SavedViewRequestValidator()
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
