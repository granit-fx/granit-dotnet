using FluentValidation;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Catalog.Endpoints.Validators;

/// <summary>
/// Validates <see cref="UpdateProductMetadataRequest"/>.
/// </summary>
internal sealed class UpdateProductMetadataRequestValidator : GranitValidator<UpdateProductMetadataRequest>
{
    /// <summary>
    /// Cap on the number of entries to bound the JSON payload size persisted in
    /// <c>MetadataJson</c> (max 4000 chars at the EF column level).
    /// </summary>
    internal const int MaxEntries = 50;

    /// <summary>Per-key length cap (matches Stripe's metadata key limit).</summary>
    internal const int MaxKeyLength = 40;

    /// <summary>Per-value length cap (matches Stripe's metadata value limit).</summary>
    internal const int MaxValueLength = 500;

    public UpdateProductMetadataRequestValidator()
    {
        RuleFor(x => x.Metadata)
            .NotNull()
            .Must(d => d.Count <= MaxEntries)
            .WithMessage($"Extra properties must contain at most {MaxEntries} entries.");

        RuleForEach(x => x.Metadata).ChildRules(entry =>
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
