using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyMetadataRequestValidator : GranitValidator<PartyMetadataRequest>
{
    public PartyMetadataRequestValidator()
    {
        // Bound the size of the dictionary to keep payloads predictable. Stripe caps at
        // 50 keys, key length 40, value length 500 — same envelope here.
        RuleFor(x => x.Metadata).NotNull();
        RuleFor(x => x.Metadata.Count).LessThanOrEqualTo(50)
            .When(x => x.Metadata is not null);
        RuleForEach(x => x.Metadata).ChildRules(child =>
        {
            child.RuleFor(kv => kv.Key).NotEmpty().MaximumLength(40);
            child.RuleFor(kv => kv.Value).MaximumLength(500);
        }).When(x => x.Metadata is not null);
    }
}

