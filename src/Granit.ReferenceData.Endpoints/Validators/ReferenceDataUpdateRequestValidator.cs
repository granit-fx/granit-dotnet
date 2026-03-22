using FluentValidation;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.ReferenceData.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="ReferenceDataUpdateRequest"/> body for reference data updates.
/// </summary>
/// <remarks>
/// MaxLength values must match <c>ReferenceDataEntityTypeConfiguration</c>: Labels = 250.
/// </remarks>
internal sealed class ReferenceDataUpdateRequestValidator : GranitValidator<ReferenceDataUpdateRequest>
{
    /// <summary>Maximum length for label fields (must match <c>ReferenceDataEntityTypeConfiguration</c>).</summary>
    internal const int MaxLabelLength = 250;

    public ReferenceDataUpdateRequestValidator()
    {
        RuleFor(x => x.LabelEn)
            .NotEmpty()
            .MaximumLength(MaxLabelLength);

        RuleFor(x => x.LabelFr).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelNl).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelDe).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelEs).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelIt).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelPt).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelZh).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelJa).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelPl).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelTr).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelKo).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelSv).MaximumLength(MaxLabelLength);
        RuleFor(x => x.LabelCs).MaximumLength(MaxLabelLength);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.ValidTo)
            .GreaterThan(x => x.ValidFrom)
            .WithErrorCodeAndMessage("Granit:Validation:ValidToAfterValidFrom")
            .When(x => x.ValidFrom.HasValue && x.ValidTo.HasValue);

        RuleFor(x => x.ParentCode)
            .MaximumLength(ReferenceDataCreateRequestValidator.MaxCodeLength)
            .When(x => x.ParentCode is not null);

        RuleForEach(x => x.ExtraProperties)
            .ChildRules(kvp =>
            {
                kvp.RuleFor(x => x.Key)
                    .NotEmpty()
                    .MaximumLength(ReferenceDataCreateRequestValidator.MaxCodeLength);

                kvp.RuleFor(x => x.Value)
                    .MaximumLength(4000);
            })
            .When(x => x.ExtraProperties is { Count: > 0 });
    }
}
