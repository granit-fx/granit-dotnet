using FluentValidation;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Catalog.Endpoints.Validators;

/// <summary>
/// Validates <see cref="UpdateProductExtraPropertiesRequest"/>.
/// </summary>
internal sealed class UpdateProductExtraPropertiesRequestValidator : GranitValidator<UpdateProductExtraPropertiesRequest>
{
    /// <summary>
    /// Cap on the number of entries to bound the JSON payload size persisted in
    /// <c>ExtraPropertiesJson</c> (max 4000 chars at the EF column level).
    /// </summary>
    internal const int MaxEntries = 50;

    /// <summary>Per-key length cap (matches Stripe's metadata key limit).</summary>
    internal const int MaxKeyLength = 40;

    /// <summary>Per-value length cap (matches Stripe's metadata value limit).</summary>
    internal const int MaxValueLength = 500;

    public UpdateProductExtraPropertiesRequestValidator()
    {
        RuleFor(x => x.ExtraProperties)
            .NotNull()
            .Must(d => d.Count <= MaxEntries)
            .WithMessage($"Extra properties must contain at most {MaxEntries} entries.");

        RuleForEach(x => x.ExtraProperties).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Key)
                .NotEmpty()
                .MaximumLength(MaxKeyLength);

            entry.RuleFor(e => e.Value)
                .NotNull()
                .MaximumLength(MaxValueLength);
        });
    }
}
