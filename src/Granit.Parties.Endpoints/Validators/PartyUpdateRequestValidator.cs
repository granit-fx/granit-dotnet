using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyUpdateRequestValidator : GranitValidator<PartyUpdateRequest>
{
    public PartyUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Website).MaximumLength(2048);
        RuleFor(x => x.Language).MaximumLength(16);
        RuleFor(x => x.Timezone).MaximumLength(64);
        RuleFor(x => x.InternalNotes).MaximumLength(8_000);
    }
}
