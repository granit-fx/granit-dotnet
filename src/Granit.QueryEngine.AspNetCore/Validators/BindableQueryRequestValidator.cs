using FluentValidation;
using Granit.QueryEngine.AspNetCore.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.QueryEngine.AspNetCore.Validators;

/// <summary>
/// Validates the <see cref="BindableQueryRequest"/> parsed from the query string.
/// Rules target the inner <see cref="QueryRequest"/> properties.
/// </summary>
internal sealed class BindableQueryRequestValidator : GranitValidator<BindableQueryRequest>
{
    internal const int MinPage = 1;
    internal const int MinPageSize = 1;
    internal const int MaxPageSize = 500;
    internal const int MaxSearchLength = 500;
    internal const int MaxSortLength = 500;
    internal const int MaxCursorLength = 2000;
    internal const int MaxGroupByLength = 200;
    internal const int MaxFilterEntries = 50;
    internal const int MaxFilterKeyLength = 200;
    internal const int MaxFilterValueLength = 2000;
    internal const int MaxQuickFilters = 20;
    internal const int MaxPresetEntries = 20;

    public BindableQueryRequestValidator()
    {
        RuleFor(x => x.Value.Page)
            .GreaterThanOrEqualTo(MinPage)
            .When(x => x.Value.Page.HasValue);

        RuleFor(x => x.Value.PageSize)
            .GreaterThanOrEqualTo(MinPageSize)
            .When(x => x.Value.PageSize.HasValue);

        RuleFor(x => x.Value.PageSize)
            .LessThanOrEqualTo(MaxPageSize)
            .When(x => x.Value.PageSize.HasValue);

        RuleFor(x => x.Value.Cursor)
            .MaximumLength(MaxCursorLength)
            .When(x => x.Value.Cursor is not null);

        RuleFor(x => x.Value.Cursor)
            .Null()
            .WithErrorCodeAndMessage("Granit:Validation:CursorPageMutuallyExclusive")
            .When(x => x.Value.Page.HasValue && x.Value.Cursor is not null);

        RuleFor(x => x.Value.Search)
            .MaximumLength(MaxSearchLength)
            .When(x => x.Value.Search is not null);

        RuleFor(x => x.Value.Sort)
            .MaximumLength(MaxSortLength)
            .When(x => x.Value.Sort is not null);

        RuleFor(x => x.Value.GroupBy)
            .MaximumLength(MaxGroupByLength)
            .When(x => x.Value.GroupBy is not null);

        RuleFor(x => x.Value.Filter)
            .Must(f => f!.Count <= MaxFilterEntries)
            .WithErrorCodeAndMessage("Granit:Validation:MaxFilterEntries")
            .When(x => x.Value.Filter is not null);

        RuleFor(x => x.Value.Filter)
            .Must(f => f!.All(kv => kv.Key.Length <= MaxFilterKeyLength
                && kv.Value.Length <= MaxFilterValueLength))
            .WithErrorCodeAndMessage("Granit:Validation:FilterEntryTooLong")
            .When(x => x.Value.Filter is not null);

        RuleFor(x => x.Value.QuickFilters)
            .Must(q => q!.Count <= MaxQuickFilters)
            .WithErrorCodeAndMessage("Granit:Validation:MaxQuickFilterEntries")
            .When(x => x.Value.QuickFilters is not null);

        RuleFor(x => x.Value.Presets)
            .Must(p => p!.Count <= MaxPresetEntries)
            .WithErrorCodeAndMessage("Granit:Validation:MaxPresetEntries")
            .When(x => x.Value.Presets is not null);
    }
}
